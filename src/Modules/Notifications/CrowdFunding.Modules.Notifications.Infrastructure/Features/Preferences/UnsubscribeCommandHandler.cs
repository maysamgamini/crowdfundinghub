using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;

/// <summary>
/// Tier 2 command handler: switches off every non-transactional notification for the user.
/// </summary>
public sealed class UnsubscribeCommandHandler : ICommandHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private readonly NotificationsDbContext _dbContext;
    private readonly INotificationsDateTimeProvider _dateTimeProvider;

    public UnsubscribeCommandHandler(NotificationsDbContext dbContext, INotificationsDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UnsubscribeResult> Handle(UnsubscribeCommand command, CancellationToken cancellationToken)
    {
        await NotificationPreferenceWriter.UpsertAsync(
            _dbContext, command.UserId, campaignUpdatesEnabled: false, marketingAnnouncementsEnabled: false,
            _dateTimeProvider.UtcNow, cancellationToken);

        return new UnsubscribeResult(command.UserId, true);
    }
}
