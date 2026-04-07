using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

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

    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        return _dbContext.Contributions
            .SingleOrDefaultAsync(x => x.Id == contributionId, cancellationToken);
    }

    public async Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        _dbContext.Contributions.Update(contribution);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
