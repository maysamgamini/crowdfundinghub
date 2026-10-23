using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;
using CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences;
using CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The Notifications preference handlers are Tier 2: they use the module's DbContext directly.
/// These tests run them against a real PostgreSQL, including two requests that arrive together
/// for a user who has no row yet.
/// </summary>
public sealed class NotificationPreferencesTier2Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("notification_preferences")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Defaults_Update_AndUnsubscribe_BehaveAsBefore()
    {
        var userId = Guid.NewGuid();

        var initial = await Get(userId);
        Assert.True(initial.CampaignUpdatesEnabled);
        Assert.False(initial.MarketingAnnouncementsEnabled);

        var updated = await Update(userId, campaignUpdates: false, marketing: true);
        Assert.False(updated.CampaignUpdatesEnabled);
        Assert.True(updated.MarketingAnnouncementsEnabled);

        var afterUpdate = await Get(userId);
        Assert.False(afterUpdate.CampaignUpdatesEnabled);
        Assert.True(afterUpdate.MarketingAnnouncementsEnabled);

        await using (var context = CreateContext())
        {
            var result = await new UnsubscribeCommandHandler(context, new FixedClock())
                .Handle(new UnsubscribeCommand(userId), CancellationToken.None);
            Assert.True(result.Unsubscribed);
        }

        var finalState = await Get(userId);
        Assert.False(finalState.CampaignUpdatesEnabled);
        Assert.False(finalState.MarketingAnnouncementsEnabled);
    }

    [Fact]
    public async Task TwoFirstUpdatesAtOnce_BothSucceed_AndLeaveOneRow()
    {
        var userId = Guid.NewGuid();

        await Task.WhenAll(
            Enumerable.Range(0, 10).Select(i => Update(userId, campaignUpdates: i % 2 == 0, marketing: i % 2 == 1)));

        await using var context = CreateContext();
        var rows = await context.NotificationPreferences.CountAsync(x => x.UserId == userId);
        Assert.Equal(1, rows);
    }

    private NotificationsDbContext CreateContext()
        => new(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options);

    private async Task<GetNotificationPreferencesResult> Get(Guid userId)
    {
        await using var context = CreateContext();
        return await new GetNotificationPreferencesQueryHandler(context, new FixedClock())
            .Handle(new GetNotificationPreferencesQuery(userId), CancellationToken.None);
    }

    private async Task<UpdateNotificationPreferencesResult> Update(Guid userId, bool campaignUpdates, bool marketing)
    {
        await using var context = CreateContext();
        return await new UpdateNotificationPreferencesCommandHandler(context, new FixedClock())
            .Handle(new UpdateNotificationPreferencesCommand(userId, campaignUpdates, marketing), CancellationToken.None);
    }

    private sealed class FixedClock : INotificationsDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
