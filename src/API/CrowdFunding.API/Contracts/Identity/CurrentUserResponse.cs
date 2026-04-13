namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload for Current User.
/// </summary>
public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
