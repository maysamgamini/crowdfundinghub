using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Pledges.Application.Features.ActiveCampaigns.Events;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The Pledges module's copy of a campaign is built from events that can arrive in any order (a redelivery, or a
/// batch handled out of order). However they arrive, the copy ends up as the last lifecycle event says.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class CampaignCacheOrderTests
{
    private readonly CrowdFundingApiFactory _factory;

    public CampaignCacheOrderTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private static CampaignCreatedApplicationEvent Created(Guid campaignId, Guid ownerId)
        => new(campaignId, ownerId, "Order Test", "USD", DateTime.UtcNow.AddDays(30));

    private async Task HandleAsync(params object[] events)
    {
        foreach (var @event in events)
        {
            // One scope per event, as the outbox worker does.
            await using var scope = _factory.Services.CreateAsyncScope();
            var handlers = scope.ServiceProvider.GetServices<CrowdFunding.BuildingBlocks.Application.Events.IEventHandler<CampaignCreatedApplicationEvent>>()
                .OfType<ReplicatedCampaignEventHandlers>().Single();

            switch (@event)
            {
                case CampaignCreatedApplicationEvent created:
                    await handlers.Handle(created, CancellationToken.None);
                    break;
                case CampaignPublishedApplicationEvent published:
                    await handlers.Handle(published, CancellationToken.None);
                    break;
                case CampaignCancelledApplicationEvent cancelled:
                    await handlers.Handle(cancelled, CancellationToken.None);
                    break;
            }
        }
    }

    private async Task<(bool IsActive, string Title, string Currency, Guid OwnerId)> RowAsync(Guid campaignId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var row = await db.ActiveCampaignsCache.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId);
        return (row.IsActive, row.Title, row.Currency, row.OwnerId);
    }

    [Fact]
    public async Task APublishedEventThatArrivesBeforeTheCreatedEvent_StillLeavesTheCampaignActive()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await HandleAsync(new CampaignPublishedApplicationEvent(campaignId, ownerId), Created(campaignId, ownerId));

        var row = await RowAsync(campaignId);
        Assert.True(row.IsActive);
        Assert.Equal("Order Test", row.Title);
        Assert.Equal("USD", row.Currency);
        Assert.Equal(ownerId, row.OwnerId);
    }

    [Fact]
    public async Task ACreatedEventDeliveredAgainAfterPublishing_DoesNotMakeTheCampaignInactive()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await HandleAsync(Created(campaignId, ownerId), new CampaignPublishedApplicationEvent(campaignId, ownerId), Created(campaignId, ownerId));

        Assert.True((await RowAsync(campaignId)).IsActive);
    }

    [Fact]
    public async Task ACancelledEventThatArrivesBeforeTheCreatedEvent_StillLeavesTheCampaignInactive()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await HandleAsync(new CampaignCancelledApplicationEvent(campaignId, ownerId), Created(campaignId, ownerId));

        Assert.False((await RowAsync(campaignId)).IsActive);
    }
}
