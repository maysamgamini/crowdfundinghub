using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

public interface IContributionRepository
{
    Task AddAsync(Contribution contribution, CancellationToken cancellationToken);
}
