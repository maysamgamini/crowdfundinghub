namespace CrowdFunding.API.Contracts.Identity;

public sealed record AssignRoleToUserResponse(
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
