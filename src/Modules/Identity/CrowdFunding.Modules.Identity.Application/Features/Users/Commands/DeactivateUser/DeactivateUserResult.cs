namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.DeactivateUser;

public sealed record DeactivateUserResult(Guid UserId, bool IsActive);
