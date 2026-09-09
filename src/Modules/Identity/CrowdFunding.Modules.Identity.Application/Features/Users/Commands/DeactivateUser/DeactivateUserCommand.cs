namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.DeactivateUser;

/// <summary>
/// Represents the request to execute the Deactivate User use case — an administrative action to
/// suspend a fraudulent creator, ban an abusive account, or terminate access for a compromised
/// account. See TICKET-049.
/// </summary>
public sealed record DeactivateUserCommand(Guid UserId);
