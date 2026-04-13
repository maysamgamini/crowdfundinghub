namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

/// <summary>
/// Represents the request to execute the Grant Permission To User use case.
/// </summary>
public sealed record GrantPermissionToUserCommand(
    Guid UserId,
    string Permission);
