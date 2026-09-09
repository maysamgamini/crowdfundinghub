namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;

/// <summary>
/// Represents the outcome returned by Refresh Access Token.
/// </summary>
public sealed record RefreshAccessTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken);
