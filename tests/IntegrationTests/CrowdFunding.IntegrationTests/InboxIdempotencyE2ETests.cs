using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Inbox;
using CrowdFunding.Modules.CampaignUpdates.Application.Events;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The generic inbox (<c>IIdempotentConsumer</c>) against real PostgreSQL: a redelivered
/// application event, sequentially or concurrently, applies its effect exactly once.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class InboxIdempotencyE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public InboxIdempotencyE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RedeliveredPledgeConfirmedEvent_ShouldEnqueueWebhookDeliveriesExactlyOnce()
    {
        var (campaignId, subscriptionIds) = await CreateCampaignWithSubscriptionsAsync(subscriptionCount: 2);
        var confirmed = new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), campaignId, Guid.NewGuid(), 25m, "USD");

        // First delivery, a sequential redelivery, then two concurrent redeliveries racing each other.
        await DeliverAsync(confirmed);
        await DeliverAsync(confirmed);
        await Task.WhenAll(DeliverAsync(confirmed), DeliverAsync(confirmed));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>();

        var deliveries = await dbContext.WebhookDeliveryTasks
            .Where(task => subscriptionIds.Contains(task.SubscriptionId))
            .CountAsync();
        Assert.Equal(subscriptionIds.Count, deliveries);

        var processed = await dbContext.Set<ProcessedEvent>()
            .Where(record => record.EventId == confirmed.EventId.ToString())
            .ToListAsync();
        var record = Assert.Single(processed);
        Assert.Equal(nameof(ContributionPaymentConfirmedActivityHandler), record.ConsumerName);
    }

    [Fact]
    public async Task DistinctEvents_ShouldEachBeApplied()
    {
        var (campaignId, subscriptionIds) = await CreateCampaignWithSubscriptionsAsync(subscriptionCount: 1);

        await DeliverAsync(new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), campaignId, Guid.NewGuid(), 10m, "USD"));
        await DeliverAsync(new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), campaignId, Guid.NewGuid(), 20m, "USD"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>();
        var deliveries = await dbContext.WebhookDeliveryTasks.CountAsync(task => subscriptionIds.Contains(task.SubscriptionId));

        Assert.Equal(2, deliveries);
    }

    /// <summary>
    /// Invokes only the CampaignUpdates consumer, each delivery in its own DI scope (and so its own
    /// DbContext and connection), exactly as separate outbox passes or replicas would.
    /// </summary>
    private async Task DeliverAsync(ContributionPaymentConfirmedApplicationEvent applicationEvent)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetServices<IEventHandler<ContributionPaymentConfirmedApplicationEvent>>()
            .OfType<ContributionPaymentConfirmedActivityHandler>()
            .Single();

        await handler.Handle(applicationEvent, CancellationToken.None);
    }

    private async Task<(Guid CampaignId, List<Guid> SubscriptionIds)> CreateCampaignWithSubscriptionsAsync(int subscriptionCount)
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("inbox-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Inbox Idempotency Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            5000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>();
        var subscriptions = Enumerable.Range(0, subscriptionCount)
            .Select(i => WebhookSubscription.Create(campaignId, $"https://hooks.example.com/{i}", "secret-key", DateTime.UtcNow))
            .ToList();
        dbContext.WebhookSubscriptions.AddRange(subscriptions);
        await dbContext.SaveChangesAsync();

        return (campaignId, subscriptions.Select(subscription => subscription.Id).ToList());
    }
}
