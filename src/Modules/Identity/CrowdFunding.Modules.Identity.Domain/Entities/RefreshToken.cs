namespace CrowdFunding.Modules.Identity.Domain.Entities;

/// <summary>
/// A single refresh token in a Refresh Token Rotation (RTR) chain (TICKET-036). Only
/// <see cref="TokenHash"/> — never the raw token — is persisted, mirroring how passwords are
/// never stored in plaintext; the raw value exists only transiently in the response returned to
/// the client at issuance time.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime issuedAtUtc, DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime issuedAtUtc, TimeSpan lifetime)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("Lifetime must be positive.", nameof(lifetime));
        }

        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, issuedAtUtc, issuedAtUtc + lifetime);
    }

    /// <summary>
    /// Whether this token can still be exchanged for a new access token as of <paramref name="now"/>.
    /// A token that fails this check for having already been revoked (as opposed to merely
    /// expired) is the reuse-detection signal: a previously-rotated-away token being presented
    /// again means the underlying secret was copied by someone other than its rightful holder.
    /// </summary>
    public bool IsActive(DateTime now) => !IsRevoked && now < ExpiresAtUtc;

    /// <summary>
    /// Revokes the token, either because it was rotated (in which case <paramref name="replacedByTokenHash"/>
    /// links to its successor, enabling reuse detection) or because of an explicit logout/breach
    /// response (in which case it is left null).
    /// </summary>
    public void Revoke(DateTime now, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        RevokedAtUtc = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
