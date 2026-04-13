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
}
