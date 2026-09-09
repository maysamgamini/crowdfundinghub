using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;

namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;

/// <summary>
/// Handles unsubscribing a user from all non-transactional / commercial notifications.
/// </summary>
public sealed class UnsubscribeCommandHandler : ICommandHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public UnsubscribeCommandHandler(
        INotificationPreferenceRepository repository,
        INotificationsDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UnsubscribeResult> Handle(UnsubscribeCommand command, CancellationToken cancellationToken)
    {
        var preference = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(command.UserId, now);
        }

        preference.Update(campaignUpdatesEnabled: false, marketingAnnouncementsEnabled: false, now);
        await _repository.UpsertAsync(preference, cancellationToken);

        return new UnsubscribeResult(command.UserId, true);
    }
}
