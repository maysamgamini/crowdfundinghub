namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for user authentication.
/// </summary>
/// <param name="Email">The registered email address of the account.</param>
/// <param name="Password">The account password.</param>
public sealed record LoginUserRequest(
    string Email,
    string Password);
