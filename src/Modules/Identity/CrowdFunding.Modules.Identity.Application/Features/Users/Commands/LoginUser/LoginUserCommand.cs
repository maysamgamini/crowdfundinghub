namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

public sealed record LoginUserCommand(
    string Email,
    string Password);
