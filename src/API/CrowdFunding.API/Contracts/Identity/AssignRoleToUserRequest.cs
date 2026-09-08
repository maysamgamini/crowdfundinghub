namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for assigning an application role to a user.
/// </summary>
/// <param name="Role">The name of the role to assign (e.g. Member, Creator, Moderator, Administrator).</param>
public sealed record AssignRoleToUserRequest(string Role);
