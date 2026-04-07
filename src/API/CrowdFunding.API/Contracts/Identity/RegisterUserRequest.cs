namespace CrowdFunding.API.Contracts.Identity;

public sealed record RegisterUserRequest(
    string Email,
    string DisplayName,
    string Password);
