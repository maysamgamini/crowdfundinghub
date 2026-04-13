namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP request payload for Assign Role To User.
/// </summary>
public sealed record AssignRoleToUserRequest(string Role);
