using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.UnitTests;

public sealed class PayoutTests
{
    private static Payout NewPayout() => Payout.Create(Guid.NewGuid(), Guid.NewGuid(), "USD", DateTime.UtcNow);

    [Fact]
    public void Prepare_TakesThePlatformFee_AndLeavesTheRestForTheCreator()
    {
        var payout = NewPayout();

        payout.Prepare(250.00m, platformFeeBps: 500);

        Assert.Equal(PayoutStatus.Ready, payout.Status);
        Assert.Equal(250.00m, payout.GrossAmount);
        Assert.Equal(12.50m, payout.FeeAmount);
        Assert.Equal(237.50m, payout.NetAmount);
        Assert.Equal(500, payout.PlatformFeeBps);
    }

    [Fact]
    public void Prepare_RoundsTheFeeToTheCent_AndTheThreeAmountsStillAddUp()
    {
        var payout = NewPayout();

        payout.Prepare(33.33m, platformFeeBps: 500);   // 1.6665 -> 1.67

        Assert.Equal(1.67m, payout.FeeAmount);
        Assert.Equal(31.66m, payout.NetAmount);
        Assert.Equal(payout.GrossAmount, payout.FeeAmount + payout.NetAmount);
    }

    [Fact]
    public void Prepare_WithNothingCollected_HasNothingToPay()
    {
        var payout = NewPayout();

        payout.Prepare(0m, platformFeeBps: 500);

        Assert.Equal(PayoutStatus.NothingToPay, payout.Status);
        Assert.Equal(0m, payout.NetAmount);
    }

    [Fact]
    public void Prepare_TwiceIsRefused_SoTheAmountCannotChangeAfterItWasFixed()
    {
        var payout = NewPayout();
        payout.Prepare(100m, 500);

        Assert.Throws<InvalidOperationException>(() => payout.Prepare(900m, 500));
        Assert.Equal(95m, payout.NetAmount);
    }

    [Fact]
    public void MarkPaid_NeedsAReadyPayout_AndPaysOnlyOnce()
    {
        var payout = NewPayout();
        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("po_1", DateTime.UtcNow));

        payout.Prepare(100m, 500);
        payout.MarkPaid("po_1", DateTime.UtcNow);

        Assert.Equal(PayoutStatus.Paid, payout.Status);
        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("po_2", DateTime.UtcNow));
        Assert.Equal("po_1", payout.ProviderPayoutId);
    }

    [Fact]
    public void ARefusedAttempt_MovesToTheNextKey_AndWaitsBeforeTryingAgain()
    {
        var payout = NewPayout();
        payout.Prepare(100m, 500);
        var retryAt = DateTime.UtcNow.AddHours(1);

        var firstKey = Payout.KeyFor(payout.CampaignId, payout.Failures + 1);
        payout.RecordFailure("account_closed", retryAt);
        var secondKey = Payout.KeyFor(payout.CampaignId, payout.Failures + 1);

        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal(PayoutStatus.Ready, payout.Status);
        Assert.Equal("account_closed", payout.LastFailureCode);
        Assert.Equal(retryAt, payout.NextAttemptAtUtc);
        Assert.EndsWith(":1", firstKey);
        Assert.EndsWith(":2", secondKey);
    }
}
