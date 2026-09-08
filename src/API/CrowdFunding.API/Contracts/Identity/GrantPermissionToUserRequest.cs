namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for granting an explicit permission to a user.
/// </summary>
/// <param name="Permission">The name of the permission to grant (e.g. campaigns:create, moderation:review).</param>
public sealed record GrantPermissionToUserRequest(string Permission);
