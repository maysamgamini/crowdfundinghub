namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for Register User.
/// </summary>
public sealed record RegisterUserRequest(
    string Email,
    string DisplayName,
    string Password);
