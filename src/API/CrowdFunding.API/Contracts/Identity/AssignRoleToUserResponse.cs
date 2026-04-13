namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload for Assign Role To User.
/// </summary>
public sealed record AssignRoleToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
