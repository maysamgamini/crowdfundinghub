using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;

public sealed class ContributionRepository : IContributionRepository
{
    private readonly ContributionsDbContext _dbContext;

    public ContributionRepository(ContributionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        await _dbContext.Contributions.AddAsync(contribution, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
