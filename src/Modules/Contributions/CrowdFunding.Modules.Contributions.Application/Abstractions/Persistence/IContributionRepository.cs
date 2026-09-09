using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for contribution aggregates.
/// </summary>
public interface IContributionRepository
{
    Task AddAsync(Contribution contribution, CancellationToken cancellationToken);
    Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken);

    /// <summary>Looks up a contribution by the external payment gateway's own reference — how a
    /// payment webhook (which knows nothing of our internal <c>ContributionId</c>) reconciles.
    /// See TICKET-033.</summary>
    Task<Contribution?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken);

    Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken);

    /// <summary>Loads every currently-Succeeded contribution for a campaign, for the refund
    /// compensation saga triggered by that campaign failing or being cancelled. Already-refunded
    /// contributions never appear here again — that is what makes re-delivering the triggering
    /// event idempotent.</summary>
    Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Loads a bounded batch of currently-Succeeded contributions for a campaign,
    /// enabling the refund compensation saga to process large campaigns in small, memory-bounded chunks
    /// without OOM or lock saturation (TICKET-055).</summary>
    Task<IReadOnlyList<Contribution>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken);
}
