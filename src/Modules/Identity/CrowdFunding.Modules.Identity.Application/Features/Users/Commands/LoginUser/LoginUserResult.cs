namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

public sealed record LoginUserResult(
    string AccessToken,
    DateTime ExpiresAtUtc);
