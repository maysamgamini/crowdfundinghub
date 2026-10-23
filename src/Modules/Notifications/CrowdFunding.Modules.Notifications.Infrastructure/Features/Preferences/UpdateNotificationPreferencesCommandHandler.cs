using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;

/// <summary>
/// Tier 2 command handler: sets a user's notification preferences straight through the module's
/// DbContext. There is no aggregate or repository, because nothing here has an invariant to guard
/// or an event to raise.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandHandler : ICommandHandler<UpdateNotificationPreferencesCommand, UpdateNotificationPreferencesResult>
{
    private readonly NotificationsDbContext _dbContext;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public UpdateNotificationPreferencesCommandHandler(NotificationsDbContext dbContext, INotificationsDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UpdateNotificationPreferencesResult> Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        await NotificationPreferenceWriter.UpsertAsync(
            _dbContext, command.UserId, command.CampaignUpdatesEnabled, command.MarketingAnnouncementsEnabled, now, cancellationToken);

        return new UpdateNotificationPreferencesResult(
            command.UserId, command.CampaignUpdatesEnabled, command.MarketingAnnouncementsEnabled, now);
    }
}
