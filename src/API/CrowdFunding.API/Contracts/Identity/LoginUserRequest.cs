namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for Login User.
/// </summary>
public sealed record LoginUserRequest(
    string Email,
    string Password);
