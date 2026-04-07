namespace CrowdFunding.API.Contracts.Identity;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
