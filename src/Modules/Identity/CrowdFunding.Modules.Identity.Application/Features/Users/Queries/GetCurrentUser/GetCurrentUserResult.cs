namespace CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;

/// <summary>
/// Represents the outcome returned by Get Current User.
/// </summary>
public sealed record GetCurrentUserResult(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> ExplicitPermissions,
    IReadOnlyCollection<string> Permissions);
