using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for pledge aggregates.
/// </summary>
public interface IPledgeRepository
{
    Task AddAsync(Pledge pledge, CancellationToken cancellationToken);

    /// <summary>Stages the record that a pledge was activated; the database refuses a second one for the same pledge.</summary>
    Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken);
    Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken);

    /// <summary>Looks up a pledge by the external payment gateway's own reference — how a
    /// payment webhook (which knows nothing of our internal <c>PledgeId</c>) reconciles.
    /// See TICKET-033.</summary>
    Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken);

    Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken);

    Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken);

    Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken);

    /// <summary>Loads every currently-Succeeded pledge for a campaign, for the refund
    /// compensation saga triggered by that campaign failing or being cancelled. Already-refunded
    /// pledges never appear here again — that is what makes re-delivering the triggering
    /// event idempotent.</summary>
    Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Loads a bounded batch of currently-Succeeded pledges for a campaign,
    /// enabling the refund compensation saga to process large campaigns in small, memory-bounded chunks
    /// without OOM or lock saturation (TICKET-055).</summary>
    Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken);
}
