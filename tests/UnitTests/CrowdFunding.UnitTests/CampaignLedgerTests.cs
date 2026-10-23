using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.ReverseContributionOnCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.Extensions.Logging.Abstractions;
using AddContributionHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign.AddContributionToCampaignCommandHandler;
using FailHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign.FailCampaignCommandHandler;
using CompleteHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign.CompleteCampaignCommandHandler;
using ReverseHandler = CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.ReverseContributionOnCampaign.ReverseContributionOnCampaignCommandHandler;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign;

namespace CrowdFunding.UnitTests;

/// <summary>
/// The campaign ledger. A pledge is a positive entry, a refund a negative compensating entry, and the exact
/// total is the sum. The events that feed it are not ordered, so a refund can be processed before the credit it
/// reverses; whichever way round they come, the ledger and the raised balance must end up the same.
/// </summary>
public sealed class CampaignLedgerTests
{
    private static readonly DateTime Deadline = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterDeadline = new(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);

    private static Campaign PublishedCampaign(decimal goal = 5000m)
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(), "Launch a school robotics lab", "This campaign funds tools and equipment for a new school robotics lab.",
            "Education", new Money(goal, "USD"), Deadline, new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        campaign.Publish(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));
        campaign.ClearDomainEvents();
        return campaign;
    }

    private static AddContributionHandler Credit(FakeCampaignRepository repository, FakeContributionLedger ledger)
        => new(repository, ledger, new FakeRewardTierRepository(), new FakeRewardTierReservationRepository(),
            new FakeCampaignTransactionExecutor(), new FakeCampaignRealtimeNotifier(), new FakeDateTimeProvider(new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc)));

    private static ReverseHandler Reverse(FakeCampaignRepository repository, FakeContributionLedger ledger)
        => new(repository, ledger, new FakeCampaignTransactionExecutor());

    [Fact]
    public async Task APledgeThenItsRefund_LeavesTheBalanceAtZero_AndTheLedgerWithTwoEntries()
    {
        var campaign = PublishedCampaign();
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        var contributionId = Guid.NewGuid();

        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, contributionId, 125m, "USD"), CancellationToken.None);
        var reversed = await Reverse(repository, ledger).Handle(new ReverseContributionOnCampaignCommand(campaign.Id, contributionId, 125m, "USD"), CancellationToken.None);

        Assert.True(reversed.Reversed);
        Assert.Equal(0m, campaign.RaisedAmount.Amount);
        Assert.Equal(0m, await ledger.SumAsync(campaign.Id, CancellationToken.None));
        Assert.Equal(2, ledger.Entries.Count);
    }

    [Fact]
    public async Task ARefundThatOvertakesItsCredit_StillEndsAtZero_InEitherOrder()
    {
        var campaign = PublishedCampaign();
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        var contributionId = Guid.NewGuid();

        // The refund event is processed first: nothing was counted yet, so the balance does not move.
        var reversed = await Reverse(repository, ledger).Handle(new ReverseContributionOnCampaignCommand(campaign.Id, contributionId, 125m, "USD"), CancellationToken.None);
        Assert.False(reversed.Reversed);
        Assert.Equal(0m, campaign.RaisedAmount.Amount);

        // The credit arrives later and finds its refund already booked, so it counts nothing.
        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, contributionId, 125m, "USD"), CancellationToken.None);

        Assert.Equal(0m, campaign.RaisedAmount.Amount);
        Assert.Equal(0m, await ledger.SumAsync(campaign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task TheSameRefundDeliveredTwice_ReversesOnce()
    {
        var campaign = PublishedCampaign();
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        var contributionId = Guid.NewGuid();
        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, contributionId, 300m, "USD"), CancellationToken.None);

        var command = new ReverseContributionOnCampaignCommand(campaign.Id, contributionId, 300m, "USD");
        var first = await Reverse(repository, ledger).Handle(command, CancellationToken.None);
        var second = await Reverse(repository, ledger).Handle(command, CancellationToken.None);

        Assert.True(first.Reversed);
        Assert.False(second.Reversed);
        Assert.Equal(0m, campaign.RaisedAmount.Amount);
    }

    [Fact]
    public async Task ARefundOnACampaignThatHasFailed_StillBooksTheEntryAndLowersTheBalance()
    {
        var campaign = PublishedCampaign(goal: 5000m);
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        var contributionId = Guid.NewGuid();
        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, contributionId, 100m, "USD"), CancellationToken.None);
        campaign.MarkFailed(AfterDeadline);

        await Reverse(repository, ledger).Handle(new ReverseContributionOnCampaignCommand(campaign.Id, contributionId, 100m, "USD"), CancellationToken.None);

        Assert.Equal(0m, campaign.RaisedAmount.Amount);
        Assert.Equal(0m, await ledger.SumAsync(campaign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task TheDeadlineDecision_UsesTheLedgerTotal_WhenTheStoredCounterHasDrifted()
    {
        var campaign = PublishedCampaign(goal: 1000m);
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, Guid.NewGuid(), 400m, "USD"), CancellationToken.None);

        // Something edited the counter behind the ledger's back: it now claims the goal was reached.
        campaign.ReconcileRaisedAmount(new Money(1000m, "USD"));
        var handler = new FailHandler(repository, new FakeDateTimeProvider(AfterDeadline), new FakeCampaignTransactionExecutor(), ledger, NullLogger<FailHandler>.Instance);

        var result = await handler.Handle(new FailCampaignCommand(campaign.Id), CancellationToken.None);

        // Decided on the ledger's 400, not the counter's 1000: the campaign failed.
        Assert.Equal("Failed", result.Status);
        Assert.Equal(400m, campaign.RaisedAmount.Amount);
    }

    [Fact]
    public async Task TheDeadlineDecision_CannotCompleteACampaign_WhoseLedgerIsBelowTheGoal_EvenIfTheCounterSaysOtherwise()
    {
        var campaign = PublishedCampaign(goal: 1000m);
        var repository = new FakeCampaignRepository(campaign);
        var ledger = new FakeContributionLedger();
        await Credit(repository, ledger).Handle(new AddContributionToCampaignCommand(campaign.Id, Guid.NewGuid(), 400m, "USD"), CancellationToken.None);
        campaign.ReconcileRaisedAmount(new Money(1000m, "USD"));
        var handler = new CompleteHandler(repository, new FakeDateTimeProvider(AfterDeadline), new FakeCampaignTransactionExecutor(), ledger, NullLogger<CompleteHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteCampaignCommand(campaign.Id), CancellationToken.None));
    }

    [Fact]
    public void ReconcilingToTheSameTotal_ReportsNoDrift()
    {
        var campaign = PublishedCampaign();

        Assert.False(campaign.ReconcileRaisedAmount(new Money(0m, "USD")));
    }
}
