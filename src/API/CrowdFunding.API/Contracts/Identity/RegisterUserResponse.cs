namespace CrowdFunding.API.Contracts.Identity;

/// <summary>
/// Represents the HTTP response payload returned after user registration.
/// </summary>
/// <param name="UserId">The unique identifier of the newly registered user.</param>
public sealed record RegisterUserResponse(Guid UserId);
