namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

/// <summary>
/// Represents the request to execute the Register User use case.
/// </summary>
public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password);
