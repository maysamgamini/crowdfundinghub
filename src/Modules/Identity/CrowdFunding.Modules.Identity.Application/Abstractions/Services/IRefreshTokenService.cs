namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// Generates and hashes opaque refresh tokens. The raw token is returned to the client exactly
/// once, at issuance/rotation time; only <see cref="Hash"/> is ever persisted, so a database leak
/// alone cannot be used to forge a valid refresh token (the same defense-in-depth principle as
/// <see cref="IPasswordHasher"/>, but keyed and compared as a plain hash rather than PBKDF2 —
/// unlike a password, the raw token is 256 bits of CSPRNG output that a client never chooses or
/// reuses across accounts, so it needs no per-token salt or slow hashing to resist brute force).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>How long a newly-issued refresh token remains exchangeable.</summary>
    TimeSpan Lifetime { get; }

    /// <summary>Generates a new cryptographically random raw refresh token.</summary>
    string GenerateToken();

    /// <summary>Computes the persisted hash of a raw refresh token.</summary>
    string Hash(string rawToken);
}
