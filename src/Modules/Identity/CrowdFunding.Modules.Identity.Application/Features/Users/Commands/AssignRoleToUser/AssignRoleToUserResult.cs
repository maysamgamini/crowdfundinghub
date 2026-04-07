namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

public sealed record AssignRoleToUserResult(
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
