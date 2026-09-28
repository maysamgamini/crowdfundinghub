namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Durable storage for an ES256 signing key's private component, so the same key survives an app
/// restart instead of being regenerated (which would invalidate every already-issued, unexpired
/// token). The private key is stored encrypted; see <c>SigningKeyProtector</c>.
/// </summary>
public sealed class SigningKeyRecord
{
    private SigningKeyRecord()
    {
    }

    /// <summary>
    /// Initializes a new active signing key record.
    /// </summary>
    /// <param name="kid">The key id published in the JWKS and in each token header.</param>
    /// <param name="protectedPrivateKey">The PKCS#8 private key as produced by <c>SigningKeyProtector.Protect</c>.</param>
    /// <param name="createdAtUtc">The UTC creation time.</param>
    public SigningKeyRecord(string kid, string protectedPrivateKey, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        Kid = kid;
        ProtectedPrivateKey = protectedPrivateKey;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    /// <summary>Gets the record id.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the key id.</summary>
    public string Kid { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the encrypted PKCS#8 private key (<c>ENC:v1:…</c>). Rows written before encryption
    /// hold plain base64 until startup re-protects them.
    /// </summary>
    public string ProtectedPrivateKey { get; private set; } = string.Empty;

    /// <summary>Gets the UTC creation time.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Gets a value indicating whether this key signs new tokens.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Retires this key as the one used to sign new tokens (TICKET-037's key rotation). The
    /// record itself is kept, not deleted — the public JWKS still needs it to verify tokens
    /// signed under this <see cref="Kid"/> before they expire.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Replaces a legacy plaintext value with its encrypted form (one-time data migration).
    /// </summary>
    /// <param name="protectedPrivateKey">The protected value.</param>
    public void ReplaceProtectedPrivateKey(string protectedPrivateKey) => ProtectedPrivateKey = protectedPrivateKey;
}
