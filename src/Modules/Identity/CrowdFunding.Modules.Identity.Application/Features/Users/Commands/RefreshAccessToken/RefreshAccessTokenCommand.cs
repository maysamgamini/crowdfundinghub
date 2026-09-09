namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;

/// <summary>
/// Represents the request to execute the Refresh Access Token use case.
/// </summary>
public sealed record RefreshAccessTokenCommand(string RefreshToken);
