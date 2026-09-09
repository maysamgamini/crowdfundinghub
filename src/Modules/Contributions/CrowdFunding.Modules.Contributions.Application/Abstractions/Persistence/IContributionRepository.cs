using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for contribution aggregates.
/// </summary>
public interface IContributionRepository
{
    Task AddAsync(Contribution contribution, CancellationToken cancellationToken);
    Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken);
    Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken);

    /// <summary>Loads every currently-Succeeded contribution for a campaign, for the refund
    /// compensation saga triggered by that campaign failing or being cancelled. Already-refunded
    /// contributions never appear here again — that is what makes re-delivering the triggering
    /// event idempotent.</summary>
    Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);
}
