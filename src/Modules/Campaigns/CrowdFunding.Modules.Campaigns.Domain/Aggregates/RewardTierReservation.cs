namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

public enum RewardTierReservationStatus
{
    /// <summary>Slot held, awaiting checkout to complete within the reservation window.</summary>
    Reserved = 1,

    /// <summary>Checkout completed — payment was confirmed for the contribution this
    /// reservation was checked out against.</summary>
    Confirmed = 2,

    /// <summary>Slot returned to availability — either the backer abandoned checkout and the
    /// scavenger reclaimed it, or the pledge behind it failed/was refunded.</summary>
    Released = 3,
}

/// <summary>
/// The record of one backer holding one <see cref="RewardTier"/> slot during checkout — the
/// correlation and expiry-tracking half of the two-phase Reserve -&gt; Confirm/Release saga that
/// <see cref="RewardTier.ReserveSlot"/>/<see cref="RewardTier.ConfirmClaim"/>/
/// <see cref="RewardTier.ReleaseReservation"/> alone cannot provide: the tier's counters record
/// how many slots are held in aggregate, but not which specific hold belongs to which backer,
/// nor when an unconfirmed hold should expire. See TICKET-043.
/// <para>
/// A reservation's id is the correlation key threaded through
/// <c>Contribution.RewardTierReservationId</c> -&gt;
/// <c>ContributionPaymentConfirmedApplicationEvent.RewardTierReservationId</c> so
/// <c>AddContributionToCampaignCommandHandler</c> can find and confirm the exact reservation a
/// confirmed pledge was checked out against, and it is what
/// <c>RewardTierReservationScavengerBackgroundService</c> queries by <see cref="ExpiresAtUtc"/>
/// to release abandoned holds back into <see cref="RewardTier.AvailableCount"/>.
/// </para>
/// </summary>
public sealed class RewardTierReservation
{
    public Guid Id { get; private set; }
    public Guid RewardTierId { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid BackerUserId { get; private set; }
    public RewardTierReservationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public Guid? ContributionId { get; private set; }

    private RewardTierReservation()
    {
    }

    private RewardTierReservation(
        Guid id, Guid rewardTierId, Guid campaignId, Guid backerUserId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Id = id;
        RewardTierId = rewardTierId;
        CampaignId = campaignId;
        BackerUserId = backerUserId;
        Status = RewardTierReservationStatus.Reserved;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>The reservation window a held slot survives without a confirmed checkout before
    /// the scavenger is allowed to reclaim it.</summary>
    public static readonly TimeSpan ReservationWindow = TimeSpan.FromMinutes(15);

    public static RewardTierReservation Create(Guid rewardTierId, Guid campaignId, Guid backerUserId, DateTime nowUtc)
    {
        if (rewardTierId == Guid.Empty)
        {
            throw new ArgumentException("RewardTierId is required.", nameof(rewardTierId));
        }

        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (backerUserId == Guid.Empty)
        {
            throw new ArgumentException("BackerUserId is required.", nameof(backerUserId));
        }

        return new RewardTierReservation(Guid.NewGuid(), rewardTierId, campaignId, backerUserId, nowUtc, nowUtc.Add(ReservationWindow));
    }

    /// <summary>Converts the hold into a permanent claim once the pledge checked out against it
    /// is confirmed. Callers are expected to call <see cref="RewardTier.ConfirmClaim"/> on the
    /// matching tier in the same transaction.</summary>
    public void Confirm(Guid contributionId)
    {
        if (Status != RewardTierReservationStatus.Reserved)
        {
            throw new InvalidOperationException($"Cannot confirm reservation '{Id}' in status '{Status}'.");
        }

        Status = RewardTierReservationStatus.Confirmed;
        ContributionId = contributionId;
    }

    /// <summary>Releases the hold — an abandoned checkout reclaimed by the scavenger, or a
    /// declined/refunded pledge. Callers are expected to call
    /// <see cref="RewardTier.ReleaseReservation"/> on the matching tier in the same
    /// transaction.</summary>
    public void Release()
    {
        if (Status != RewardTierReservationStatus.Reserved)
        {
            throw new InvalidOperationException($"Cannot release reservation '{Id}' in status '{Status}'.");
        }

        Status = RewardTierReservationStatus.Released;
    }

    /// <summary>
    /// Reverses a confirmed reservation when the corresponding contribution is refunded (TICKET-051).
    /// Transitions status from <see cref="RewardTierReservationStatus.Confirmed"/> to
    /// <see cref="RewardTierReservationStatus.Released"/>.
    /// </summary>
    public void Refund()
    {
        if (Status != RewardTierReservationStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot refund reservation '{Id}' in status '{Status}'.");
        }

        Status = RewardTierReservationStatus.Released;
    }

    public bool IsExpired(DateTime nowUtc) => Status == RewardTierReservationStatus.Reserved && ExpiresAtUtc <= nowUtc;
}
