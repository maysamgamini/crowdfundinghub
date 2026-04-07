namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

public sealed record AssignRoleToUserCommand(
    Guid UserId,
    string Role);
