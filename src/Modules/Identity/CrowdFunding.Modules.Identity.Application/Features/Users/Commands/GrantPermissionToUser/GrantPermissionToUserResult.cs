namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

/// <summary>
/// Represents the outcome returned by Grant Permission To User.
/// </summary>
public sealed record GrantPermissionToUserResult(
    Guid UserId,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
