using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Events.LatePledgeRejected;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;
using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using AddPledgeHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddPledgeToCampaign.AddPledgeToCampaignCommandHandler;

namespace CrowdFunding.UnitTests;

/// <summary>
/// The closing fence. A payment confirmed after its campaign ended must not be counted, must not be
/// retried into the dead-letter table, and must not leave the backer charged: the campaign refuses it
/// with an event and Pledges refunds it.
/// </summary>
public sealed class LatePledgeTests
{
    private static readonly DateTime Now = new(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc);

    private static Campaign PublishedCampaign()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(), "Launch a school robotics lab", "This campaign funds tools and equipment for a new school robotics lab.",
            "Education", new Money(5000m, "USD"), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        campaign.Publish(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));
        campaign.ClearDomainEvents();
        return campaign;
    }

    private static AddPledgeHandler HandlerFor(Campaign campaign, FakePledgeLedger ledger, FakeCampaignRepository repository)
        => new(repository, ledger, new FakeRewardTierRepository(), new FakeRewardTierReservationRepository(),
            new FakeCampaignTransactionExecutor(), new FakeCampaignRealtimeNotifier(), new FakeDateTimeProvider(Now));

    [Fact]
    public async Task APaymentConfirmedAfterTheCampaignWasCancelled_IsRejectedNotCounted_AndAnEventIsRaised()
    {
        var campaign = PublishedCampaign();
        campaign.Cancel();
        campaign.ClearDomainEvents();
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakePledgeLedger();
        var pledgeId = Guid.NewGuid();

        var result = await HandlerFor(campaign, ledger, repository)
            .Handle(new AddPledgeToCampaignCommand(campaign.Id, pledgeId, 125m, "usd"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(0m, result.RaisedAmount);
        // The ledger tells the whole story: the payment arrived and the campaign refused it, so the two entries cancel.
        Assert.Collection(ledger.Entries.OrderBy(e => e.Amount),
            refund => { Assert.Equal("Refund", refund.Kind); Assert.Equal(-125m, refund.Amount); },
            pledge => { Assert.Equal("Pledge", pledge.Kind); Assert.Equal(125m, pledge.Amount); });
        Assert.Equal(0m, await ledger.SumAsync(campaign.Id, CancellationToken.None));
        Assert.Equal(new Money(0m, "USD"), campaign.RaisedAmount);
        var rejected = Assert.IsType<LatePledgeRejectedDomainEvent>(Assert.Single(campaign.DomainEvents));
        Assert.Equal(pledgeId, rejected.PledgeId);
        Assert.Equal("Cancelled", rejected.CampaignStatus);
        Assert.True(repository.WasUpdated);
    }

    [Fact]
    public async Task APaymentConfirmedWhilePublished_IsStillCountedAsBefore()
    {
        var campaign = PublishedCampaign();
        var result = await HandlerFor(campaign, new FakePledgeLedger(), new FakeCampaignRepository(campaign))
            .Handle(new AddPledgeToCampaignCommand(campaign.Id, Guid.NewGuid(), 125m, "usd"), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(125m, result.RaisedAmount);
        Assert.Empty(campaign.DomainEvents.OfType<LatePledgeRejectedDomainEvent>());
    }

    [Fact]
    public void APublishedCampaign_CannotRejectAPledge()
    {
        var campaign = PublishedCampaign();

        Assert.Throws<InvalidOperationException>(() =>
            campaign.RejectLatePledge(Guid.NewGuid(), new Money(10m, "USD"), Now));
    }

    private static LatePledgeRejectedApplicationEvent RejectionOf(Guid pledgeId, Guid campaignId)
        => new(campaignId, pledgeId, 100m, "USD", "Cancelled", Now);

    private static Pledge SucceededPledge()
    {
        var pledge = Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", Now);
        pledge.ConfirmPayment("ref-1", Now);
        pledge.ClearDomainEvents();
        return pledge;
    }

    private static LatePledgeRefundHandler RefundHandler(FakePledgeRepository repository)
        => new(repository, new PledgeRefunder(repository, new FakePledgeTransactionExecutor(), new FakePledgeDateTimeProvider(Now), new FakePaymentProvider(), new FakeRefundStore([]), new FakePaymentSettings()));

    [Fact]
    public async Task ARejectedPledge_IsRefunded()
    {
        var pledge = SucceededPledge();

        await RefundHandler(new FakePledgeRepository(pledge))
            .Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);

        Assert.Equal(PledgeStatus.Refunded, pledge.Status);
    }

    [Fact]
    public async Task TheSameRejectionDeliveredTwice_RefundsOnce()
    {
        var pledge = SucceededPledge();
        var handler = RefundHandler(new FakePledgeRepository(pledge));

        await handler.Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);
        pledge.ClearDomainEvents();
        await handler.Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);

        Assert.Equal(PledgeStatus.Refunded, pledge.Status);
        Assert.Empty(pledge.DomainEvents);
    }

    [Fact]
    public async Task APledgeThatNeverSucceeded_OrDoesNotExist_IsLeftAlone()
    {
        var pending = Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", Now);

        await RefundHandler(new FakePledgeRepository(pending)).Handle(RejectionOf(pending.Id, pending.CampaignId), CancellationToken.None);
        await RefundHandler(new FakePledgeRepository()).Handle(RejectionOf(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(PledgeStatus.Pending, pending.Status);
    }
}
