using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;

/// <summary>
/// Tier 2 query handler: reads the user's preferences straight from the module's DbContext and
/// returns the defaults when the user has never saved any.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler : IQueryHandler<GetNotificationPreferencesQuery, GetNotificationPreferencesResult>
{
    private readonly NotificationsDbContext _dbContext;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public GetNotificationPreferencesQueryHandler(NotificationsDbContext dbContext, INotificationsDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<GetNotificationPreferencesResult> Handle(GetNotificationPreferencesQuery query, CancellationToken cancellationToken)
    {
        var preference = await _dbContext.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == query.UserId, cancellationToken)
            ?? NotificationPreference.CreateDefault(query.UserId, _dateTimeProvider.UtcNow);

        return new GetNotificationPreferencesResult(
            preference.UserId,
            preference.CampaignUpdatesEnabled,
            preference.MarketingAnnouncementsEnabled,
            preference.UpdatedAtUtc);
    }
}
