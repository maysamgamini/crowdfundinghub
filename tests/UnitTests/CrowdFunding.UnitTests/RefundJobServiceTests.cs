using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrowdFunding.UnitTests;

/// <summary>
/// The refund job: rate limited (a few refunds a pass), resumable (the next pass carries on), every answered attempt
/// recorded, a refusal waits out a delay and then uses a new key, and a pledge that keeps being refused ends the job
/// as "needs attention" instead of looping for ever.
/// </summary>
public sealed class RefundJobServiceTests
{
    private static readonly DateTime Start = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private sealed class MovableClock(DateTime now) : CrowdFunding.Modules.Pledges.Application.Abstractions.Services.IPledgeDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = now;
    }

    private sealed record Setup(
        RefundJobService Service,
        FakeRefundStore Store,
        FakePaymentProvider Provider,
        MovableClock Clock,
        List<Pledge> Pledges,
        Guid CampaignId);

    private static Setup Build(int pledgeCount, int refundsPerPass = 50, int maxAttempts = 5)
    {
        var campaignId = Guid.NewGuid();
        var pledges = Enumerable.Range(0, pledgeCount).Select(i =>
        {
            var pledge = Pledge.Create(campaignId, Guid.NewGuid(), 10m, "USD", Start.AddSeconds(i));
            pledge.AttachPaymentIntent($"pi_{i}", "Mock");
            pledge.ConfirmPayment($"ref-{i}", Start);
            pledge.ClearDomainEvents();
            return pledge;
        }).ToList();

        var settings = new FakePaymentSettings { RefundsPerPass = refundsPerPass, MaxRefundAttempts = maxAttempts };
        var store = new FakeRefundStore(pledges);
        store.Jobs.Add(RefundJob.Create(campaignId, Start));
        var provider = new FakePaymentProvider();
        var clock = new MovableClock(Start);
        var executor = new FakePledgeTransactionExecutor();
        var refunder = new PledgeRefunder(new FakeRefundablePledgeRepository(pledges), executor, clock, provider, store, settings);
        var service = new RefundJobService(store, refunder, executor, clock, settings, NullLogger<RefundJobService>.Instance);
        return new Setup(service, store, provider, clock, pledges, campaignId);
    }

    [Fact]
    public async Task APass_StartsAtMostTheRateLimit_AndTheNextPassCarriesOn()
    {
        var setup = Build(pledgeCount: 5, refundsPerPass: 2);

        var first = await setup.Service.RefundDueAsync(2, CancellationToken.None);

        Assert.Equal(2, first);
        Assert.Equal(2, setup.Pledges.Count(x => x.Status == PledgeStatus.Refunded));
        Assert.Equal(RefundJobStatus.Open, setup.Store.Jobs.Single().Status);

        await setup.Service.RefundDueAsync(2, CancellationToken.None);
        await setup.Service.RefundDueAsync(2, CancellationToken.None);

        Assert.All(setup.Pledges, x => Assert.Equal(PledgeStatus.Refunded, x.Status));
        Assert.Equal(RefundJobStatus.Completed, setup.Store.Jobs.Single().Status);
        Assert.Equal(5, setup.Provider.RefundsPerformed);
    }

    [Fact]
    public async Task EveryAnsweredAttempt_IsRecorded_WithItsKey()
    {
        var setup = Build(pledgeCount: 2);

        await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(2, setup.Store.Attempts.Count);
        Assert.All(setup.Store.Attempts, attempt =>
        {
            Assert.Equal(RefundAttemptOutcome.Succeeded, attempt.Outcome);
            Assert.Equal(PledgeRefunder.RefundKey(attempt.PledgeId, 1), attempt.IdempotencyKey);
        });
    }

    [Fact]
    public async Task ARefusedRefund_WaitsOutTheRetryDelay_ThenUsesANewKey()
    {
        var setup = Build(pledgeCount: 1);
        var pledge = setup.Pledges.Single();
        setup.Provider.FailRefundsWith("account_closed");

        await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(PledgeStatus.Succeeded, pledge.Status);
        Assert.Equal(1, pledge.RefundFailures);
        var failed = Assert.Single(setup.Store.Attempts);
        Assert.Equal(RefundAttemptOutcome.Failed, failed.Outcome);
        Assert.Equal("account_closed", failed.FailureCode);

        // Before the retry delay has passed, the pledge is left alone.
        setup.Provider.FailRefundsWith(null);
        var tooEarly = await setup.Service.RefundDueAsync(10, CancellationToken.None);
        Assert.Equal(0, tooEarly);

        setup.Clock.UtcNow = Start.AddMinutes(16);
        await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(PledgeStatus.Refunded, pledge.Status);
        Assert.Equal(PledgeRefunder.RefundKey(pledge.Id, 2), setup.Store.Attempts.Last().IdempotencyKey);
        Assert.Equal(RefundJobStatus.Completed, setup.Store.Jobs.Single().Status);
    }

    [Fact]
    public async Task APledgeThatKeepsBeingRefused_EndsTheJobAsNeedsAttention_AndIsNotRetriedForEver()
    {
        var setup = Build(pledgeCount: 2, maxAttempts: 2);
        setup.Provider.FailRefundsWith("do_not_honor");

        // Two passes, each after the retry delay: the second refusal is the last allowed attempt.
        await setup.Service.RefundDueAsync(10, CancellationToken.None);
        setup.Clock.UtcNow = Start.AddMinutes(16);
        await setup.Service.RefundDueAsync(10, CancellationToken.None);
        setup.Clock.UtcNow = Start.AddMinutes(40);
        var third = await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(0, third);
        Assert.Equal(RefundJobStatus.NeedsAttention, setup.Store.Jobs.Single().Status);
        Assert.All(setup.Pledges, x => Assert.Equal(2, x.RefundFailures));
    }

    [Fact]
    public async Task OneRefusedPledge_DoesNotStopTheOthersInTheSamePass()
    {
        var setup = Build(pledgeCount: 3);
        var refused = setup.Pledges[0];

        // Only the first pledge's payment reference is refused.
        setup.Provider.FailRefundsWith("card_expired");
        await setup.Service.RefundDueAsync(1, CancellationToken.None);
        setup.Provider.FailRefundsWith(null);
        await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(PledgeStatus.Succeeded, refused.Status);
        Assert.All(setup.Pledges.Skip(1), x => Assert.Equal(PledgeStatus.Refunded, x.Status));
        Assert.Equal(RefundJobStatus.Open, setup.Store.Jobs.Single().Status);
    }

    [Fact]
    public async Task ARepeatedPass_NeverRefundsAPledgeTwice()
    {
        var setup = Build(pledgeCount: 3);

        await setup.Service.RefundDueAsync(10, CancellationToken.None);
        await setup.Service.RefundDueAsync(10, CancellationToken.None);

        Assert.Equal(3, setup.Provider.RefundsPerformed);
        Assert.Equal(3, setup.Provider.RefundCalls);
    }

    [Fact]
    public async Task TheRateLimit_IsSharedAcrossAllOpenJobs_NotAppliedToEachJobOnItsOwn()
    {
        var setup = Build(pledgeCount: 3);

        // A second campaign with its own open job and three paid pledges.
        var otherCampaign = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            var pledge = Pledge.Create(otherCampaign, Guid.NewGuid(), 10m, "USD", Start.AddSeconds(100 + i));
            pledge.AttachPaymentIntent($"pi_other_{i}", "Mock");
            pledge.ConfirmPayment($"ref-other-{i}", Start);
            pledge.ClearDomainEvents();
            setup.Pledges.Add(pledge);
        }

        setup.Store.Jobs.Add(RefundJob.Create(otherCampaign, Start.AddMinutes(1)));

        var started = await setup.Service.RefundDueAsync(4, CancellationToken.None);

        Assert.Equal(4, started);
        Assert.Equal(4, setup.Pledges.Count(x => x.Status == PledgeStatus.Refunded));
    }
}
