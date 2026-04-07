namespace CrowdFunding.API.Contracts.Identity;

public sealed record LoginUserResponse(
    string AccessToken,
    DateTime ExpiresAtUtc);
