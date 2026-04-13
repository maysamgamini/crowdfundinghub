namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload for Register User.
/// </summary>
public sealed record RegisterUserResponse(Guid UserId);
