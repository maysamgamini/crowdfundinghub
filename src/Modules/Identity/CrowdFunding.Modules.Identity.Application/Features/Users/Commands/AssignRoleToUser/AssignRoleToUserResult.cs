namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

/// <summary>
/// Represents the outcome returned by Assign Role To User.
/// </summary>
public sealed record AssignRoleToUserResult(
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
