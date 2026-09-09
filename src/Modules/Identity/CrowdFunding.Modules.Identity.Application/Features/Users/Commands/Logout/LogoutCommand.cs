namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;

/// <summary>
/// Represents the request to execute the Logout use case.
/// </summary>
public sealed record LogoutCommand(string RefreshToken);
