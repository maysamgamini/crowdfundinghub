using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for Contribution.
/// </summary>
public sealed class ContributionRepository : IContributionRepository
{
    private readonly ContributionsDbContext _dbContext;

    public ContributionRepository(ContributionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        await _dbContext.Contributions.AddAsync(contribution, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        return _dbContext.Contributions
            .SingleOrDefaultAsync(x => x.Id == contributionId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        _dbContext.Contributions.Update(contribution);
        return Task.CompletedTask;
    }
}
