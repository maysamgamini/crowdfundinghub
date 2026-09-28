namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// The claim type names this platform writes into access tokens and reads back. Every host
/// validates tokens with inbound claim mapping switched off, so these are the exact names on the
/// wire (<c>sub</c>, not <c>ClaimTypes.NameIdentifier</c>'s schema URI).
/// </summary>
public static class CustomClaimTypes
{
    /// <summary>
    /// The registered JWT subject (RFC 7519 §4.1.2): the user id. The only claim any host reads
    /// to identify the caller.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>The user's e-mail address (OpenID Connect standard claim).</summary>
    public const string Email = "email";

    /// <summary>One claim per role the user holds; also the tokens' role claim type.</summary>
    public const string Role = "role";

    /// <summary>The user's display name.</summary>
    public const string DisplayName = "display_name";

    /// <summary>One claim per permission; authorization policies require these.</summary>
    public const string Permission = "permission";

    /// <summary>
    /// Carries the issuing <c>User.SecurityStamp</c> value at the moment the token was signed
    /// (TICKET-036). Every module checks this claim against the distributed revocation
    /// blacklist rather than the JWT's own lifetime alone, so that logout, refresh-token-reuse
    /// detection, and account deactivation can all invalidate an already-issued, not-yet-expired
    /// access token without a database round-trip on every request.
    /// </summary>
    public const string SecurityStamp = "security_stamp";
}
