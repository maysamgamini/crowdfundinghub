using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
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
    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        // Add (not AddAsync) — EF's AddAsync exists only for value generators that need async DB
        // access (e.g. SQL Server HiLo), which Contribution's client-generated Guid key doesn't use.
        _dbContext.Contributions.Add(contribution);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        return _dbContext.Contributions
            .SingleOrDefaultAsync(x => x.Id == contributionId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Contribution?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
    {
        return _dbContext.Contributions
            .SingleOrDefaultAsync(x => x.ExternalPaymentIntentId == externalPaymentIntentId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        _dbContext.Contributions.Update(contribution);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.Contributions
            .Where(x => x.CampaignId == campaignId && x.Status == ContributionStatus.Succeeded)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Contribution>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken)
    {
        return await _dbContext.Contributions
            .Where(x => x.CampaignId == campaignId && x.Status == ContributionStatus.Succeeded)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
