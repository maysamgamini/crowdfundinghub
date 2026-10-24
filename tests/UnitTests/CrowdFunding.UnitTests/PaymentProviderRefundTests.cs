using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Payments;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Refunds go through the payment provider, and the provider is called outside the database transaction. These
/// tests pin down the part that keeps money right: the idempotency key. The same key returns the first answer
/// (so a retry after a timeout or a crash cannot refund twice), and a new attempt gets a new key only after the
/// provider has answered "no" (because a provider replays a failed answer for a repeated key).
/// </summary>
public sealed class PaymentProviderRefundTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Contribution SucceededPledge(decimal amount = 100m)
    {
        var pledge = Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "USD", Now);
        pledge.AttachPaymentIntent($"pi_mock_{pledge.Id:N}", "Mock");
        pledge.ConfirmPayment("ref", Now);
        pledge.ClearDomainEvents();
        return pledge;
    }

    private static (ContributionRefunder Refunder, FakePaymentProvider Provider, FakeContributionTransactionExecutor Executor, FakeContributionRepository Repository) Build(Contribution pledge)
    {
        var provider = new FakePaymentProvider();
        var executor = new FakeContributionTransactionExecutor();
        var repository = new FakeContributionRepository(pledge);
        return (new ContributionRefunder(repository, executor, new FakeContributionDateTimeProvider(Now), provider), provider, executor, repository);
    }

    [Fact]
    public async Task TheSameKey_GetsTheFirstAnswer_AndTheProviderActsOnce()
    {
        var provider = new FakePaymentProvider();

        var first = await provider.RefundAsync("pi_1", 50m, "USD", "key-1", CancellationToken.None);
        var second = await provider.RefundAsync("pi_1", 50m, "USD", "key-1", CancellationToken.None);

        Assert.Equal(RefundOutcome.Succeeded, first.Outcome);
        Assert.Equal(first.ProviderRefundId, second.ProviderRefundId);
        Assert.Equal(2, provider.RefundCalls);
        Assert.Equal(1, provider.RefundsPerformed);
    }

    [Fact]
    public async Task ADifferentKey_IsANewRefund()
    {
        var provider = new FakePaymentProvider();

        var first = await provider.RefundAsync("pi_1", 50m, "USD", "key-1", CancellationToken.None);
        var second = await provider.RefundAsync("pi_1", 50m, "USD", "key-2", CancellationToken.None);

        Assert.NotEqual(first.ProviderRefundId, second.ProviderRefundId);
        Assert.Equal(2, provider.RefundsPerformed);
    }

    [Fact]
    public async Task AFailedAnswer_IsReplayedForTheSameKey_ButNotForANewOne()
    {
        var provider = new FakePaymentProvider();
        provider.FailRefundsWith("insufficient_funds");
        var failed = await provider.RefundAsync("pi_1", 50m, "USD", "key-1", CancellationToken.None);

        provider.FailRefundsWith(null);
        var replay = await provider.RefundAsync("pi_1", 50m, "USD", "key-1", CancellationToken.None);
        var freshAttempt = await provider.RefundAsync("pi_1", 50m, "USD", "key-2", CancellationToken.None);

        Assert.Equal(RefundOutcome.Failed, failed.Outcome);
        Assert.Equal(RefundOutcome.Failed, replay.Outcome);
        Assert.Equal(RefundOutcome.Succeeded, freshAttempt.Outcome);
    }

    [Fact]
    public async Task ARefund_AsksTheProvider_ThenMarksThePledgeRefundedWithTheProvidersReference()
    {
        var pledge = SucceededPledge();
        var (refunder, provider, executor, _) = Build(pledge);

        await refunder.RefundAsync(pledge, CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, pledge.Status);
        Assert.StartsWith("re_mock_", pledge.RefundReference);
        Assert.Equal(1, provider.RefundsPerformed);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task WhenTheProviderSaysNo_ThePledgeStaysSucceeded_AndTheFailureIsRemembered()
    {
        var pledge = SucceededPledge();
        var (refunder, provider, _, _) = Build(pledge);
        provider.FailRefundsWith("insufficient_funds");

        await Assert.ThrowsAsync<PaymentProviderException>(() => refunder.RefundAsync(pledge, CancellationToken.None));

        Assert.Equal(ContributionStatus.Succeeded, pledge.Status);
        Assert.Equal(1, pledge.RefundFailures);
        Assert.Equal("insufficient_funds", pledge.LastRefundFailure);
        Assert.Null(pledge.RefundReference);
    }

    [Fact]
    public async Task TheRetryAfterARefusal_IsANewAttemptWithANewKey_AndCanSucceed()
    {
        var pledge = SucceededPledge();
        var (refunder, provider, _, _) = Build(pledge);
        provider.FailRefundsWith("insufficient_funds");
        await Assert.ThrowsAsync<PaymentProviderException>(() => refunder.RefundAsync(pledge, CancellationToken.None));

        provider.FailRefundsWith(null);
        await refunder.RefundAsync(pledge, CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, pledge.Status);
        Assert.Equal(1, pledge.RefundFailures);
        Assert.Equal(1, provider.RefundsPerformed);
    }

    [Fact]
    public async Task ACrashAfterTheProviderActed_AndBeforeTheStatusChanged_DoesNotRefundTwice()
    {
        var pledge = SucceededPledge();
        var provider = new FakePaymentProvider();

        // The first run: the provider refunds, then the process dies before the pledge is marked.
        await provider.RefundAsync($"pi_mock_{pledge.Id:N}", 100m, "USD", ContributionRefunder.RefundKey(pledge.Id, 1), CancellationToken.None);
        Assert.Equal(ContributionStatus.Succeeded, pledge.Status);

        // The retry runs the whole refund again, with the same key.
        var repository = new FakeContributionRepository(pledge);
        var refunder = new ContributionRefunder(repository, new FakeContributionTransactionExecutor(), new FakeContributionDateTimeProvider(Now), provider);
        await refunder.RefundAsync(pledge, CancellationToken.None);

        Assert.Equal(ContributionStatus.Refunded, pledge.Status);
        Assert.Equal(2, provider.RefundCalls);
        Assert.Equal(1, provider.RefundsPerformed);
    }

    [Fact]
    public void TheKey_ChangesWithTheAttempt_AndNotWithAnythingElse()
    {
        var id = Guid.NewGuid();

        Assert.Equal(ContributionRefunder.RefundKey(id, 1), ContributionRefunder.RefundKey(id, 1));
        Assert.NotEqual(ContributionRefunder.RefundKey(id, 1), ContributionRefunder.RefundKey(id, 2));
        Assert.NotEqual(ContributionRefunder.RefundKey(id, 1), ContributionRefunder.RefundKey(Guid.NewGuid(), 1));
    }
}
