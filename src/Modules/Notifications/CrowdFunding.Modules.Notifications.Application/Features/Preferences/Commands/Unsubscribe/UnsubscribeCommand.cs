namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;

/// <summary>
/// Command to unsubscribe a user from all non-transactional / commercial notifications (CAN-SPAM / GDPR).
/// </summary>
public sealed record UnsubscribeCommand(Guid UserId);

/// <summary>
/// Result of the unsubscribe operation.
/// </summary>
public sealed record UnsubscribeResult(Guid UserId, bool Unsubscribed);
