namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned after granting an explicit permission.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="ExplicitPermissions">The updated collection of explicit permissions granted to the user.</param>
/// <param name="Permissions">The updated collection of effective permissions granted to the user.</param>
public sealed record GrantPermissionToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
