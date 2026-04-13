namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

/// <summary>
/// Represents the outcome returned by Login User.
/// </summary>
public sealed record LoginUserResult(
    string AccessToken,
    DateTime ExpiresAtUtc);
