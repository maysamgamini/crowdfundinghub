namespace CrowdFunding.API.Contracts.Identity;

public sealed record GrantPermissionToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
