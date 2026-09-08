namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned after assigning an application role.
/// </summary>
/// <param name="UserId">The unique identifier of the updated user.</param>
/// <param name="Roles">The updated collection of roles assigned to the user.</param>
/// <param name="Permissions">The updated collection of effective permissions granted to the user.</param>
public sealed record AssignRoleToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
