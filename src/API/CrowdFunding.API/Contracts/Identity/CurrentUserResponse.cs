namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload containing the profile and permissions of the authenticated user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's public display name.</param>
/// <param name="Roles">Collection of roles assigned to the user.</param>
/// <param name="ExplicitPermissions">Collection of permissions explicitly granted directly to the user.</param>
/// <param name="Permissions">Effective flattened collection of all permissions available to the user.</param>
public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
