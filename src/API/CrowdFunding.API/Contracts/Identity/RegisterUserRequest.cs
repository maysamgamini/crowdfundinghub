namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for registering a new user account.
/// </summary>
/// <param name="Email">The user's unique email address used for login and notifications.</param>
/// <param name="DisplayName">The user's public display name visible across campaigns and contributions.</param>
/// <param name="Password">The plain-text password satisfying security complexity requirements.</param>
public sealed record RegisterUserRequest(
    string Email,
    string DisplayName,
    string Password);
