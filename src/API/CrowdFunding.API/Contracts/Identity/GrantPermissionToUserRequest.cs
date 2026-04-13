namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for Grant Permission To User.
/// </summary>
public sealed record GrantPermissionToUserRequest(string Permission);
