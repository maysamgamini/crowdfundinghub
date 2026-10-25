using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Data with a useful life must not grow for ever. These tests age rows in a real database and run the
/// retention schedule once, to see that old idempotency keys and old inbox rows go and recent ones stay.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RetentionE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public RetentionE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private Task RunRetentionAsync()
    {
        var service = _factory.Services.GetServices<IHostedService>().OfType<RetentionBackgroundService>().Single();
        return service.RunOnceAsync(DateTime.UtcNow, CancellationToken.None);
    }

    [Fact]
    public async Task AnIdempotencyKeyOlderThanTheWindow_IsCleared_AndTheRecentOneAndThePledgesStay()
    {
        var campaignId = Guid.NewGuid();
        var oldPledge = Pledge.Create(campaignId, Guid.NewGuid(), 10m, "USD", DateTime.UtcNow.AddDays(-8), idempotencyKey: $"old-{Guid.NewGuid():N}");
        var recentPledge = Pledge.Create(campaignId, Guid.NewGuid(), 10m, "USD", DateTime.UtcNow.AddDays(-2), idempotencyKey: $"recent-{Guid.NewGuid():N}");
        var recentKey = recentPledge.IdempotencyKey;

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<PledgesDbContext>();
            db.Pledges.AddRange(oldPledge, recentPledge);
            await db.SaveChangesAsync();
        }

        await RunRetentionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var old = await check.Pledges.AsNoTracking().SingleAsync(x => x.Id == oldPledge.Id);
        var recent = await check.Pledges.AsNoTracking().SingleAsync(x => x.Id == recentPledge.Id);
        Assert.Null(old.IdempotencyKey);
        Assert.Equal(10m, old.Money.Amount);
        Assert.Equal(recentKey, recent.IdempotencyKey);
    }

    [Fact]
    public async Task InboxRowsOlderThanTheWindow_AreDeleted_AndRecentOnesStay()
    {
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<CampaignsDbContext>();
            db.Set<InboxMessage>().AddRange(
                new InboxMessage { MessageId = oldId, Consumer = "Test.Consumer", ProcessedAtUtc = DateTime.UtcNow.AddDays(-15) },
                new InboxMessage { MessageId = recentId, Consumer = "Test.Consumer", ProcessedAtUtc = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        await RunRetentionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        Assert.False(await check.Set<InboxMessage>().AnyAsync(x => x.MessageId == oldId));
        Assert.True(await check.Set<InboxMessage>().AnyAsync(x => x.MessageId == recentId));
    }

    [Fact]
    public async Task PaymentWebhookRecordsOlderThanTheWindow_AreDeleted_AndRecentOnesStay()
    {
        var oldId = $"evt_old_{Guid.NewGuid():N}";
        var recentId = $"evt_recent_{Guid.NewGuid():N}";

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<PledgesDbContext>();
            db.ProcessedPaymentWebhooks.AddRange(
                new ProcessedPaymentWebhook { WebhookEventId = oldId, PaymentIntentId = "pi_1", ProcessedAtUtc = DateTime.UtcNow.AddDays(-8) },
                new ProcessedPaymentWebhook { WebhookEventId = recentId, PaymentIntentId = "pi_2", ProcessedAtUtc = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        await RunRetentionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        Assert.False(await check.ProcessedPaymentWebhooks.AnyAsync(x => x.WebhookEventId == oldId));
        Assert.True(await check.ProcessedPaymentWebhooks.AnyAsync(x => x.WebhookEventId == recentId));
    }
}
