namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload for Grant Permission To User.
/// </summary>
public sealed record GrantPermissionToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
