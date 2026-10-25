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

    /// <summary>The sum of the pledges that were really charged.</summary>
    Task<decimal> SumChargedAsync(Guid campaignId, CancellationToken cancellationToken);
}
