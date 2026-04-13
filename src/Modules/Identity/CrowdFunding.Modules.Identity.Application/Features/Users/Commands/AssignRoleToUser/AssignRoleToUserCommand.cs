namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

/// <summary>
/// Represents the request to execute the Assign Role To User use case.
/// </summary>
public sealed record AssignRoleToUserCommand(
    Guid UserId,
    string Role);
