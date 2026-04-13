namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload for Login User.
/// </summary>
public sealed record LoginUserResponse(
    string AccessToken,
    DateTime ExpiresAtUtc);
