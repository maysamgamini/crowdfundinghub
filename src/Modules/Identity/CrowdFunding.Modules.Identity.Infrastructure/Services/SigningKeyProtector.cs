using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Encrypts ES256 private signing keys at rest with AES-256-GCM (ported from apps/backend's
/// <c>SigningKeyEncryption</c>, hardened: the key must be configured, and each ciphertext is
/// bound to its key id).
/// </summary>
/// <remarks>
/// <para>
/// Without this, anyone who can read <c>identity_signing_keys</c> (a backup, a replica, a
/// leaked dump) can mint valid access tokens for every user. The encryption key lives outside
/// the database, in configuration: <c>SigningKeyEncryption:Key</c> (environment variable
/// <c>SigningKeyEncryption__Key</c>), a base64-encoded 32-byte key from a secret store.
/// </para>
/// <para>
/// Stored format: <c>ENC:v1:</c> + base64(nonce[12] | tag[16] | ciphertext). The key id is
/// passed as GCM associated data, so a ciphertext copied onto another row fails authentication
/// instead of silently decrypting under the wrong <c>kid</c>. The version prefix leaves room to
/// rotate the encryption key or algorithm later.
/// </para>
/// </remarks>
public sealed class SigningKeyProtector
{
    /// <summary>The configuration key holding the base64-encoded 32-byte encryption key.</summary>
    public const string ConfigurationKey = "SigningKeyEncryption:Key";

    /// <summary>The prefix that marks a protected value (and its format version).</summary>
    public const string ProtectedPrefix = "ENC:v1:";

    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKeyProtector"/> class.
    /// </summary>
    /// <param name="configuration">Configuration holding <see cref="ConfigurationKey"/>.</param>
    /// <exception cref="InvalidOperationException">The key is missing, not base64, or not 32 bytes.</exception>
    public SigningKeyProtector(IConfiguration configuration)
    {
        var encoded = configuration[ConfigurationKey];

        // Fail fast at startup: silently falling back to a built-in key would leave the private
        // keys protected by a value published in this repository.
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException(
                $"'{ConfigurationKey}' is not configured. Provide a base64-encoded 32-byte key (e.g. `openssl rand -base64 32`).");
        }

        try
        {
            _key = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"'{ConfigurationKey}' must be base64-encoded.");
        }

        if (_key.Length != KeySizeBytes)
        {
            throw new InvalidOperationException($"'{ConfigurationKey}' must decode to exactly {KeySizeBytes} bytes (AES-256).");
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="storedValue"/> is already protected.
    /// Values written before encryption was introduced are plain base64 PKCS#8.
    /// </summary>
    /// <param name="storedValue">The value read from the database.</param>
    /// <returns>Whether the value carries the protected prefix.</returns>
    public static bool IsProtected(string storedValue) => storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Encrypts a PKCS#8 private key for storage.
    /// </summary>
    /// <param name="kid">The key id, bound to the ciphertext as associated data.</param>
    /// <param name="pkcs8PrivateKey">The private key bytes.</param>
    /// <returns>The protected value to store.</returns>
    public string Protect(string kid, ReadOnlySpan<byte> pkcs8PrivateKey)
    {
        var payload = new byte[NonceSizeBytes + TagSizeBytes + pkcs8PrivateKey.Length];
        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes);
        var ciphertext = payload.AsSpan(NonceSizeBytes + TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, pkcs8PrivateKey, ciphertext, tag, Encoding.UTF8.GetBytes(kid));

        return ProtectedPrefix + Convert.ToBase64String(payload);
    }

    /// <summary>
    /// Decrypts a stored private key. A legacy unprotected value (plain base64 PKCS#8) is decoded
    /// as is, so rows written before encryption still load until they are re-protected.
    /// </summary>
    /// <param name="kid">The key id the value was protected with.</param>
    /// <param name="storedValue">The value read from the database.</param>
    /// <returns>The PKCS#8 private key bytes.</returns>
    /// <exception cref="CryptographicException">The value was tampered with, belongs to another kid, or the encryption key is wrong.</exception>
    public byte[] Unprotect(string kid, string storedValue)
    {
        if (!IsProtected(storedValue))
        {
            return Convert.FromBase64String(storedValue);
        }

        var payload = Convert.FromBase64String(storedValue[ProtectedPrefix.Length..]);
        if (payload.Length <= NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Protected signing key payload is too short.");
        }

        var plaintext = new byte[payload.Length - NonceSizeBytes - TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(
            payload.AsSpan(0, NonceSizeBytes),
            payload.AsSpan(NonceSizeBytes + TagSizeBytes),
            payload.AsSpan(NonceSizeBytes, TagSizeBytes),
            plaintext,
            Encoding.UTF8.GetBytes(kid));

        return plaintext;
    }
}
