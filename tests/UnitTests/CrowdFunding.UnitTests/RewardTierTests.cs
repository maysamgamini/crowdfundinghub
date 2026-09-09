using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.UnitTests;

/// <summary>TICKET-034: the RewardTier state machine's own invariants, independent of the
/// PostgreSQL advisory-lock/xmin concurrency defenses verified at the integration level.</summary>
public sealed class RewardTierTests
{
    private static RewardTier CreateTier(int totalCapacity = 5)
        => RewardTier.Create(Guid.NewGuid(), "Early Bird", "Limited edition", new Money(199m, "USD"), totalCapacity, DateTime.UtcNow);

    [Fact]
    public void Create_ShouldInitializeWithZeroClaimedAndReserved()
    {
        var tier = CreateTier(totalCapacity: 50);

        Assert.Equal(50, tier.TotalCapacity);
        Assert.Equal(0, tier.ClaimedCount);
        Assert.Equal(0, tier.ReservedCount);
        Assert.Equal(50, tier.AvailableCount);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTotalCapacityIsNotPositive()
        => Assert.Throws<ArgumentException>(() => RewardTier.Create(Guid.NewGuid(), "Tier", "Desc", new Money(1m, "USD"), 0, DateTime.UtcNow));

    [Fact]
    public void ReserveSlot_ShouldIncrementReservedCount_AndDecrementAvailability()
    {
        var tier = CreateTier(totalCapacity: 5);

        tier.ReserveSlot();

        Assert.Equal(1, tier.ReservedCount);
        Assert.Equal(4, tier.AvailableCount);
    }

    [Fact]
    public void ReserveSlot_ShouldThrow_WhenNoSlotsRemain()
    {
        var tier = CreateTier(totalCapacity: 1);
        tier.ReserveSlot();

        Assert.Throws<InvalidOperationException>(() => tier.ReserveSlot());
    }

    [Fact]
    public void ReserveSlot_ShouldReturnTrue_OnlyForTheReservationThatExhaustsTheLastSlot()
    {
        var tier = CreateTier(totalCapacity: 2);

        Assert.False(tier.ReserveSlot());
        Assert.True(tier.ReserveSlot());
    }

    [Fact]
    public void ConfirmClaim_ShouldMoveASlotFromReservedToClaimed()
    {
        var tier = CreateTier(totalCapacity: 5);
        tier.ReserveSlot();

        tier.ConfirmClaim();

        Assert.Equal(0, tier.ReservedCount);
        Assert.Equal(1, tier.ClaimedCount);
        Assert.Equal(4, tier.AvailableCount);
    }

    [Fact]
    public void ConfirmClaim_ShouldThrow_WhenNothingIsReserved()
    {
        var tier = CreateTier();

        Assert.Throws<InvalidOperationException>(() => tier.ConfirmClaim());
    }

    [Fact]
    public void ReleaseReservation_ShouldReturnASlotToAvailability()
    {
        var tier = CreateTier(totalCapacity: 5);
        tier.ReserveSlot();

        tier.ReleaseReservation();

        Assert.Equal(0, tier.ReservedCount);
        Assert.Equal(5, tier.AvailableCount);
    }

    [Fact]
    public void ReleaseReservation_ShouldThrow_WhenNothingIsReserved()
    {
        var tier = CreateTier();

        Assert.Throws<InvalidOperationException>(() => tier.ReleaseReservation());
    }

    [Fact]
    public void ClaimedPlusReserved_ShouldNeverExceedTotalCapacity_UnderAnySequenceOfOperations()
    {
        var tier = CreateTier(totalCapacity: 3);

        tier.ReserveSlot();
        tier.ReserveSlot();
        tier.ConfirmClaim();
        tier.ReserveSlot();

        Assert.True(tier.ClaimedCount + tier.ReservedCount <= tier.TotalCapacity);
        Assert.Equal(0, tier.AvailableCount);
        Assert.Throws<InvalidOperationException>(() => tier.ReserveSlot());
    }
}
