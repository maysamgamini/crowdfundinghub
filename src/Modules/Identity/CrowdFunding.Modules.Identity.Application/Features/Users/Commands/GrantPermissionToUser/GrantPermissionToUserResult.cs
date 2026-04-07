namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

public sealed record GrantPermissionToUserResult(
    Guid UserId,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
