using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;

namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences;

/// <summary>
/// Handles retrieving user notification preferences, returning defaults if not yet configured.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler : IQueryHandler<GetNotificationPreferencesQuery, GetNotificationPreferencesResult>
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public GetNotificationPreferencesQueryHandler(
        INotificationPreferenceRepository repository,
        INotificationsDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<GetNotificationPreferencesResult> Handle(GetNotificationPreferencesQuery query, CancellationToken cancellationToken)
    {
        var preference = await _repository.GetByUserIdAsync(query.UserId, cancellationToken)
            ?? NotificationPreference.CreateDefault(query.UserId, _dateTimeProvider.UtcNow);

        return new GetNotificationPreferencesResult(
            preference.UserId,
            preference.CampaignUpdatesEnabled,
            preference.MarketingAnnouncementsEnabled,
            preference.UpdatedAtUtc);
    }
}
