using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Events.LateContributionRejected;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using AddContributionHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign.AddContributionToCampaignCommandHandler;

namespace CrowdFunding.UnitTests;

/// <summary>
/// The closing fence. A payment confirmed after its campaign ended must not be counted, must not be
/// retried into the dead-letter table, and must not leave the backer charged: the campaign refuses it
/// with an event and Contributions refunds it.
/// </summary>
public sealed class LateContributionTests
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

    private static AddContributionHandler HandlerFor(Campaign campaign, FakeContributionLedger ledger, FakeCampaignRepository repository)
        => new(repository, ledger, new FakeRewardTierRepository(), new FakeRewardTierReservationRepository(),
            new FakeCampaignTransactionExecutor(), new FakeCampaignRealtimeNotifier(), new FakeDateTimeProvider(Now));

    [Fact]
    public async Task APaymentConfirmedAfterTheCampaignWasCancelled_IsRejectedNotCounted_AndAnEventIsRaised()
    {
        var campaign = PublishedCampaign();
        campaign.Cancel();
        campaign.ClearDomainEvents();
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        var contributionId = Guid.NewGuid();

        var result = await HandlerFor(campaign, ledger, repository)
            .Handle(new AddContributionToCampaignCommand(campaign.Id, contributionId, 125m, "usd"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(0m, result.RaisedAmount);
        Assert.Equal(0, ledger.RecordAttemptCount);
        Assert.Equal(new Money(0m, "USD"), campaign.RaisedAmount);
        var rejected = Assert.IsType<LateContributionRejectedDomainEvent>(Assert.Single(campaign.DomainEvents));
        Assert.Equal(contributionId, rejected.ContributionId);
        Assert.Equal("Cancelled", rejected.CampaignStatus);
        Assert.True(repository.WasUpdated);
    }

    [Fact]
    public async Task APaymentConfirmedWhilePublished_IsStillCountedAsBefore()
    {
        var campaign = PublishedCampaign();
        var result = await HandlerFor(campaign, new FakeContributionLedger(), new FakeCampaignRepository(campaign))
            .Handle(new AddContributionToCampaignCommand(campaign.Id, Guid.NewGuid(), 125m, "usd"), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(125m, result.RaisedAmount);
        Assert.Empty(campaign.DomainEvents.OfType<LateContributionRejectedDomainEvent>());
    }

    [Fact]
    public void APublishedCampaign_CannotRejectAContribution()
    {
        var campaign = PublishedCampaign();

        Assert.Throws<InvalidOperationException>(() =>
            campaign.RejectLateContribution(Guid.NewGuid(), new Money(10m, "USD"), Now));
    }

    private static LateContributionRejectedApplicationEvent RejectionOf(Guid contributionId, Guid campaignId)
        => new(campaignId, contributionId, 100m, "USD", "Cancelled", Now);

    private static Contribution SucceededPledge()
    {
        var pledge = Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", Now);
        pledge.ConfirmPayment("ref-1", Now);
        pledge.ClearDomainEvents();
        return pledge;
    }

    private static LateContributionRefundHandler RefundHandler(FakeContributionRepository repository)
        => new(repository, new FakeContributionTransactionExecutor(), new FakeContributionDateTimeProvider(Now));

    [Fact]
    public async Task ARejectedContribution_IsRefunded()
    {
        var pledge = SucceededPledge();

        await RefundHandler(new FakeContributionRepository(pledge))
            .Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, pledge.Status);
    }

    [Fact]
    public async Task TheSameRejectionDeliveredTwice_RefundsOnce()
    {
        var pledge = SucceededPledge();
        var handler = RefundHandler(new FakeContributionRepository(pledge));

        await handler.Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);
        pledge.ClearDomainEvents();
        await handler.Handle(RejectionOf(pledge.Id, pledge.CampaignId), CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, pledge.Status);
        Assert.Empty(pledge.DomainEvents);
    }

    [Fact]
    public async Task APledgeThatNeverSucceeded_OrDoesNotExist_IsLeftAlone()
    {
        var pending = Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", Now);

        await RefundHandler(new FakeContributionRepository(pending)).Handle(RejectionOf(pending.Id, pending.CampaignId), CancellationToken.None);
        await RefundHandler(new FakeContributionRepository()).Handle(RejectionOf(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ContributionStatus.Pending, pending.Status);
    }
}
