namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;

/// <summary>
/// Represents the outcome returned by Logout. Carries no data — its purpose is the side effect
/// (refresh token revocation and security-stamp rotation), not a response payload.
/// </summary>
public sealed record LogoutResult;
