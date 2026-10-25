using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Events;

namespace CrowdFunding.UnitTests;

public sealed class PayoutTests
{
    private static Payout NewPayout() => Payout.Create(Guid.NewGuid(), Guid.NewGuid(), "USD", DateTime.UtcNow);

    [Fact]
    public void Prepare_TakesThePlatformFee_AndLeavesTheRestForTheCreator()
    {
        var payout = NewPayout();

        payout.Prepare([250.00m], platformFeeBps: 500);

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

        payout.Prepare([33.33m], platformFeeBps: 500);   // 1.6665 -> 1.67

        Assert.Equal(1.67m, payout.FeeAmount);
        Assert.Equal(31.66m, payout.NetAmount);
        Assert.Equal(payout.GrossAmount, payout.FeeAmount + payout.NetAmount);
    }

    [Fact]
    public void Prepare_WithNothingCollected_HasNothingToPay()
    {
        var payout = NewPayout();

        payout.Prepare([0m], platformFeeBps: 500);

        Assert.Equal(PayoutStatus.NothingToPay, payout.Status);
        Assert.Equal(0m, payout.NetAmount);
    }

    [Fact]
    public void Prepare_TwiceIsRefused_SoTheAmountCannotChangeAfterItWasFixed()
    {
        var payout = NewPayout();
        payout.Prepare([100m], 500);

        Assert.Throws<InvalidOperationException>(() => payout.Prepare([900m], 500));
        Assert.Equal(95m, payout.NetAmount);
    }

    [Fact]
    public void MarkPaid_NeedsAReadyPayout_AndPaysOnlyOnce()
    {
        var payout = NewPayout();
        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("po_1", DateTime.UtcNow));

        payout.Prepare([100m], 500);
        payout.MarkPaid("po_1", DateTime.UtcNow);

        Assert.Equal(PayoutStatus.Paid, payout.Status);
        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("po_2", DateTime.UtcNow));
        Assert.Equal("po_1", payout.ProviderPayoutId);
    }

    [Fact]
    public void ARefusedAttempt_MovesToTheNextKey_AndWaitsBeforeTryingAgain()
    {
        var payout = NewPayout();
        payout.Prepare([100m], 500);
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

    [Fact]
    public void TheFeeIsTheSumOfTheFeesOfTheSinglePledges_SoItAgreesWithTheMeteredFees()
    {
        var payout = Payout.Create(Guid.NewGuid(), Guid.NewGuid(), "USD", DateTime.UtcNow);

        // Each 33.33 carries a fee of 1.6665, which is 1.67 to the cent; the total 99.99 on its own would give 5.00.
        payout.Prepare([33.33m, 33.33m, 33.33m], platformFeeBps: 500);

        Assert.Equal(99.99m, payout.GrossAmount);
        Assert.Equal(5.01m, payout.FeeAmount);
        Assert.Equal(94.98m, payout.NetAmount);
    }

    [Fact]
    public void ThePlatformFeeOfOnePledge_IsRoundedToTheCentAwayFromZero()
    {
        Assert.Equal(1.67m, PlatformFee.ForPledge(33.33m, 500));
        Assert.Equal(12.50m, PlatformFee.ForPledge(250m, 500));
        Assert.Equal(0.01m, PlatformFee.ForPledge(0.10m, 500));
    }

    [Fact]
    public void MarkPaid_RaisesAPayoutPaidEvent_WithTheFixedAmounts()
    {
        var payout = NewPayout();
        payout.Prepare([200m], 500);

        payout.MarkPaid("po_1", DateTime.UtcNow);

        var raised = Assert.IsType<PayoutPaidDomainEvent>(Assert.Single(payout.DomainEvents));
        Assert.Equal(payout.Id, raised.PayoutId);
        Assert.Equal(200m, raised.GrossAmount);
        Assert.Equal(10m, raised.FeeAmount);
        Assert.Equal(190m, raised.NetAmount);
    }
}
