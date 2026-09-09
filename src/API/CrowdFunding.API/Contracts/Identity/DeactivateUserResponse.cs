namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned upon deactivating a user.
/// </summary>
public sealed record DeactivateUserResponse(Guid UserId, bool IsActive);
