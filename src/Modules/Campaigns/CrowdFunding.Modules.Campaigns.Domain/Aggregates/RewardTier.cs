using CrowdFunding.BuildingBlocks.Domain.ValueObjects;

namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

/// <summary>
/// A limited-quantity reward perk tier for a campaign (e.g. "Early Bird — 50 backers at $199").
/// Protects the one invariant that matters under concurrent checkout —
/// <c>ClaimedCount + ReservedCount &lt;= TotalCapacity</c> — which a bare CRUD counter update
/// cannot: without an aggregate boundary, two concurrent readers both observe capacity remaining
/// and both write a successful increment, overselling the tier. See TICKET-034.
/// <para>
/// Deliberately does not raise domain events: reservation is a same-process, same-transaction
/// concern (see <c>ReserveRewardTierSlotCommandHandler</c>) with no other module needing to react
/// to it durably today — the sold-out real-time broadcast follows the same inline,
/// best-effort-not-durable pattern <c>AddContributionToCampaignCommandHandler</c> already uses
/// for pledge updates via <c>ICampaignRealtimeNotifier</c>, rather than round-tripping through
/// the outbox for a UI-only notification.
/// </para>
/// </summary>
public sealed class RewardTier
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money MinimumPledgeAmount { get; private set; } = null!;
    public int TotalCapacity { get; private set; }
    public int ClaimedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public int AvailableCount => TotalCapacity - (ClaimedCount + ReservedCount);

    private RewardTier()
    {
    }

    private RewardTier(
        Guid id, Guid campaignId, string title, string description, Money minimumPledgeAmount, int totalCapacity, DateTime createdAtUtc)
    {
        Id = id;
        CampaignId = campaignId;
        Title = title;
        Description = description;
        MinimumPledgeAmount = minimumPledgeAmount;
        TotalCapacity = totalCapacity;
        ClaimedCount = 0;
        ReservedCount = 0;
        CreatedAtUtc = createdAtUtc;
    }

    public static RewardTier Create(
        Guid campaignId, string title, string description, Money minimumPledgeAmount, int totalCapacity, DateTime createdAtUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (totalCapacity <= 0)
        {
            throw new ArgumentException("Total capacity must be greater than zero.", nameof(totalCapacity));
        }

        return new RewardTier(Guid.NewGuid(), campaignId, title.Trim(), description?.Trim() ?? string.Empty, minimumPledgeAmount, totalCapacity, createdAtUtc);
    }

    /// <summary>
    /// Reserves one slot. Callers are expected to hold a <c>pg_advisory_xact_lock</c> keyed by
    /// this tier's id for the duration of the surrounding transaction (see
    /// <c>ReserveRewardTierSlotCommandHandler</c>) — that lock, not this check alone, is what
    /// makes the invariant hold under real concurrency: without it, two transactions can both
    /// read <see cref="AvailableCount"/> as positive before either commits its increment.
    /// </summary>
    /// <returns><see langword="true"/> if this reservation was the one that exhausted the last
    /// available slot — the caller's signal to broadcast a sold-out notification.</returns>
    public bool ReserveSlot()
    {
        if (AvailableCount <= 0)
        {
            throw new InvalidOperationException($"Reward tier '{Title}' is completely sold out.");
        }

        ReservedCount++;

        return AvailableCount == 0;
    }

    /// <summary>Releases a previously reserved slot back into availability — an abandoned
    /// checkout or an expired reservation window.</summary>
    public void ReleaseReservation()
    {
        if (ReservedCount <= 0)
        {
            throw new InvalidOperationException("No reserved slot to release for this tier.");
        }

        ReservedCount--;
    }

    /// <summary>Converts a reservation into a claim once the pledge behind it is confirmed.</summary>
    public void ConfirmClaim()
    {
        if (ReservedCount <= 0)
        {
            throw new InvalidOperationException("No reserved slot to confirm for this tier.");
        }

        ReservedCount--;
        ClaimedCount++;
    }
}
