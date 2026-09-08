namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;

/// <summary>
/// Represents the result of an administrative user seeding operation.
/// </summary>
/// <param name="UserId">The unique identifier of the administrator user account.</param>
/// <param name="WasNewlyCreated">True if a new account was created; false if an existing account was promoted to Admin.</param>
public sealed record SeedAdminResult(Guid UserId, bool WasNewlyCreated);
