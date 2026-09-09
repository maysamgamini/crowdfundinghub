namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Defines custom claim type names stored in JWT tokens and authorization policies.
/// </summary>
public static class CustomClaimTypes
{
    public const string DisplayName = "display_name";
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
