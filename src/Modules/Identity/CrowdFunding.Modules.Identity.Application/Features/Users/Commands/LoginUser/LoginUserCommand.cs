namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

/// <summary>
/// Represents the request to execute the Login User use case.
/// </summary>
public sealed record LoginUserCommand(
    string Email,
    string Password);
