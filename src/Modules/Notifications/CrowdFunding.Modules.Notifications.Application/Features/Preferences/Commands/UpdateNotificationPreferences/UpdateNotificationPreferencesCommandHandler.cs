using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;

namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;

/// <summary>
/// Handles updating user notification preferences.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandHandler : ICommandHandler<UpdateNotificationPreferencesCommand, UpdateNotificationPreferencesResult>
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public UpdateNotificationPreferencesCommandHandler(
        INotificationPreferenceRepository repository,
        INotificationsDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UpdateNotificationPreferencesResult> Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        var preference = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(command.UserId, now);
        }

        preference.Update(command.CampaignUpdatesEnabled, command.MarketingAnnouncementsEnabled, now);
        await _repository.UpsertAsync(preference, cancellationToken);

        return new UpdateNotificationPreferencesResult(
            preference.UserId,
            preference.CampaignUpdatesEnabled,
            preference.MarketingAnnouncementsEnabled,
            preference.UpdatedAtUtc);
    }
}
