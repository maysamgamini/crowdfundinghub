namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;

public sealed record SeedAdminResult(Guid UserId, bool WasNewlyCreated);
