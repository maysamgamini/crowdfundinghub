namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

public sealed record GrantPermissionToUserCommand(
    Guid UserId,
    string Permission);
