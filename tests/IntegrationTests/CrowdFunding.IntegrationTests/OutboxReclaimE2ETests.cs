using System.Net;
using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// A worker claims a batch, marks the rows Processing with a lease, and then dies before it finishes.
/// The rows must not stay stuck: once the lease has run out, another worker claims them again.
/// The test seeds the exact state a crashed worker leaves behind and observes what a healthy worker does.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OutboxReclaimE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public OutboxReclaimE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Writes a CampaignCreated event into the Campaigns outbox in the state a dead worker would have left.</summary>
    private async Task<(Guid MessageId, Guid CampaignId)> SeedStuckMessageAsync(DateTime leaseEndsUtc, int attempts)
    {
        var campaignId = Guid.NewGuid();
        var message = OutboxMessage.Create(
            new CampaignCreatedApplicationEvent(campaignId, Guid.NewGuid(), "Reclaimed Campaign", "USD", DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddMinutes(-5));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE campaigns.campaigns_outbox_messages
            SET "Status" = 1, "LockedBy" = 'worker-that-died', "LockedUntilUtc" = {1}, "Attempts" = {2}
            WHERE "Id" = {0}
            """,
            message.Id, leaseEndsUtc, attempts);

        return (message.Id, campaignId);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var adminEmail = ApiTestExtensions.UniqueEmail("reclaim-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var client = _factory.CreateClient();
        client.SetBearerToken(await client.LoginUserAsync(adminEmail));
        return client;
    }

    [Fact]
    public async Task ARowWhoseLeaseHasRunOut_IsReclaimedAndDelivered()
    {
        var (messageId, campaignId) = await SeedStuckMessageAsync(DateTime.UtcNow.AddMinutes(-1), attempts: 0);
        using var admin = await AdminClientAsync();

        await _factory.ProcessOutboxMessagesAsync();

        // Delivered: Moderation reacted to the event, and the row left the active table.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/moderation/reviews/{campaignId}")).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        Assert.False(await db.OutboxMessages.AnyAsync(m => m.Id == messageId));
    }

    [Fact]
    public async Task ARowWhoseLeaseIsStillRunning_IsLeftAlone()
    {
        var (messageId, campaignId) = await SeedStuckMessageAsync(DateTime.UtcNow.AddHours(1), attempts: 0);
        using var admin = await AdminClientAsync();

        await _factory.ProcessOutboxMessagesAsync();

        // The worker that holds it may simply be slow, so nobody else may take it yet.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/moderation/reviews/{campaignId}")).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);
        Assert.Equal(OutboxMessageStatus.Processing, row.Status);
        Assert.Equal("worker-that-died", row.LockedBy);
        Assert.Equal(0, row.Attempts);
    }

    [Fact]
    public async Task AMessageThatKeepsKillingItsWorker_EndsInTheDeadLetterTable()
    {
        // Four earlier claims never finished; this reclaim is the fifth.
        var (messageId, campaignId) = await SeedStuckMessageAsync(DateTime.UtcNow.AddMinutes(-1), attempts: 4);
        using var admin = await AdminClientAsync();

        await _factory.ProcessOutboxMessagesAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/moderation/reviews/{campaignId}")).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        Assert.False(await db.OutboxMessages.AnyAsync(m => m.Id == messageId));
        var deadLetter = await db.DeadLetterEvents.AsNoTracking().SingleAsync(d => d.SourceEventId == messageId);
        Assert.Contains("worker died", deadLetter.FailureReason);
    }

    [Fact]
    public async Task AClaim_WritesItsLeaseOnTheApplicationClock()
    {
        var message = OutboxMessage.Create(
            new CampaignCreatedApplicationEvent(Guid.NewGuid(), Guid.NewGuid(), "Lease Clock", "USD", DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow.AddMinutes(-1));
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
            seedDb.OutboxMessages.Add(message);
            await seedDb.SaveChangesAsync();
        }

        // A fresh context: the one that inserted the row would hand back its own stale tracked copy.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();

        var before = DateTime.UtcNow;
        var claimed = await db.ClaimPendingBatchAsync("campaigns_outbox_messages", "test-worker", 500, TimeSpan.FromSeconds(30), CancellationToken.None);
        var after = DateTime.UtcNow;

        var mine = claimed.Single(m => m.Id == message.Id);
        Assert.InRange(mine.LockedUntilUtc!.Value, before.AddSeconds(30), after.AddSeconds(30));
        Assert.Equal(OutboxMessageStatus.Processing, mine.Status);
    }
}
