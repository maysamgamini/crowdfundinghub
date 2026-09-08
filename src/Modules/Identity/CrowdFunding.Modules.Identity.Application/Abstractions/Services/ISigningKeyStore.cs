using System.Security.Cryptography;

namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// A single ES256 (ECDSA P-256) signing key, identified by its <paramref name="Kid"/> (JWT
/// header "kid"). <see cref="Key"/> holds the full private key when used for signing, or only
/// the public components when exposed via JWKS — callers must never serialize a
/// <see cref="Key"/> obtained from <see cref="ISigningKeyStore.GetPublicSigningKeys"/> in a way
/// that could export its private parameters (it is only ever constructed from the public half
/// to begin with, so there is nothing to leak).
/// </summary>
public sealed record SigningKeyMaterial(string Kid, ECDsa Key);

/// <summary>
/// Provides the asymmetric (ES256) key(s) used to sign and verify access tokens
/// (improvement.md §2.8/§3.8 #2). Replaces the previous symmetric HMAC-SHA256 secret, which had
/// to be shared with every verifier (API gateways, downstream services) — anyone holding it
/// could forge tokens. With an asymmetric key, only this service ever holds the private key;
/// verifiers need only the public half, published at <c>/.well-known/jwks.json</c>.
/// </summary>
public interface ISigningKeyStore
{
    /// <summary>
    /// Loads (generating one if none exists yet) the active signing key into an in-memory cache.
    /// Must be called once during application startup before <see cref="GetActiveSigningKey"/>
    /// or <see cref="GetPublicSigningKeys"/> are used, since both read synchronously from that
    /// cache — key material does not change within a running process, so there is no benefit to
    /// re-querying the database on every token issued or verified.
    /// </summary>
    Task WarmUpAsync(CancellationToken cancellationToken);

    /// <summary>The current key, with its private component, used to sign new tokens.</summary>
    SigningKeyMaterial GetActiveSigningKey();

    /// <summary>Every known key with only its public component populated, for JWKS publication
    /// and token verification.</summary>
    IReadOnlyList<SigningKeyMaterial> GetPublicSigningKeys();
}
