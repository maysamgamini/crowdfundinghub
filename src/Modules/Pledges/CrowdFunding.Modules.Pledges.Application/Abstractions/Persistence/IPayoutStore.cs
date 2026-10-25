using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

/// <summary>Persistence for creator payouts and the two questions a payout asks of the pledges.</summary>
public interface IPayoutStore
{
    Task<bool> ExistsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Stages a new payout; the database refuses a second one for the same campaign.</summary>
    Task AddAsync(Payout payout, CancellationToken cancellationToken);

    /// <summary>Payouts that are waiting or ready and whose next attempt time has come, oldest first.</summary>
    Task<IReadOnlyList<Payout>> GetDueAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken);

    Task UpdateAsync(Payout payout, CancellationToken cancellationToken);

    /// <summary>How many pledges of the campaign are still on their way to being charged (saved, queued or in flight).</summary>
    Task<int> CountUnsettledPledgesAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Each pledge of the campaign that was really charged, with its amount.</summary>
    Task<IReadOnlyList<ChargedPledge>> GetChargedPledgesAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Stages lines of a payout's append-only ledger.</summary>
    Task AddLedgerEntriesAsync(IEnumerable<PayoutLedgerEntry> entries, CancellationToken cancellationToken);

    /// <summary>
    /// Pledges that were counted in a payout and have been refunded since, and whose refund the payout's ledger
    /// does not hold yet.
    /// </summary>
    Task<IReadOnlyList<RefundedAfterPayout>> GetRefundedAfterPayoutAsync(int limit, CancellationToken cancellationToken);

    Task<Payout?> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PayoutLedgerEntry>> GetLedgerAsync(Guid payoutId, CancellationToken cancellationToken);
}

/// <summary>A pledge that was charged, as the payout sees it.</summary>
public sealed record ChargedPledge(Guid PledgeId, decimal Amount);

/// <summary>A pledge counted in a payout that was refunded afterwards.</summary>
public sealed record RefundedAfterPayout(Guid PayoutId, Guid PledgeId, decimal Amount, decimal PlatformFee, string Currency);
