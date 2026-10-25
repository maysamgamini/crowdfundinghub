using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for Pledge.
/// </summary>
public sealed class PledgeRepository : IPledgeRepository
{
    private readonly PledgesDbContext _dbContext;

    public PledgeRepository(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken)
    {
        _dbContext.PledgeActivations.Add(activation);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AddAsync(Pledge pledge, CancellationToken cancellationToken)
    {
        // Add (not AddAsync) — EF's AddAsync exists only for value generators that need async DB
        // access (e.g. SQL Server HiLo), which Pledge's client-generated Guid key doesn't use.
        _dbContext.Pledges.Add(pledge);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken)
    {
        return _dbContext.Pledges
            .SingleOrDefaultAsync(x => x.Id == pledgeId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken)
    {
        return _dbContext.Pledges
            .SingleOrDefaultAsync(x => x.ExternalPaymentIntentId == externalPaymentIntentId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        return _dbContext.Pledges
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.BackerId == backerId && x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken)
    {
        return _dbContext.Pledges
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CampaignId == campaignId
                     && x.BackerId == backerId
                     && (x.Status == PledgeStatus.Pending || x.Status == PledgeStatus.Succeeded),
                cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken)
    {
        _dbContext.Pledges.Update(pledge);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.Pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken)
    {
        return await _dbContext.Pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
