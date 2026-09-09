namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for logging out and revoking the current session.
/// </summary>
/// <param name="RefreshToken">The refresh token to revoke.</param>
public sealed record LogoutRequest(string RefreshToken);
