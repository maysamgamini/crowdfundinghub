namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password);
