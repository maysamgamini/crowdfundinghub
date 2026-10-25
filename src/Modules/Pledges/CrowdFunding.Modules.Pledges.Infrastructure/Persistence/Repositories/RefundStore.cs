using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

public sealed class RefundStore : IRefundStore
{
    private readonly PledgesDbContext _dbContext;

    public RefundStore(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> JobExistsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
        => _dbContext.RefundJobs.AnyAsync(x => x.CampaignId == campaignId, cancellationToken);

    public async Task AddJobAsync(RefundJob job, CancellationToken cancellationToken)
        => await _dbContext.RefundJobs.AddAsync(job, cancellationToken);

    public async Task<IReadOnlyList<RefundJob>> GetOpenJobsAsync(int limit, CancellationToken cancellationToken)
        => await _dbContext.RefundJobs
            .AsTracking()
            .Where(x => x.Status == RefundJobStatus.Open)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task UpdateJobAsync(RefundJob job, CancellationToken cancellationToken)
    {
        _dbContext.RefundJobs.Update(job);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Pledge>> GetRefundablePledgesAsync(
        Guid campaignId, DateTime nowUtc, int maxAttempts, int limit, CancellationToken cancellationToken)
        => await _dbContext.Pledges
            .AsTracking()
            .Where(x => x.CampaignId == campaignId
                        && x.Status == PledgeStatus.Succeeded
                        && x.RefundFailures < maxAttempts
                        && (x.NextRefundAtUtc == null || x.NextRefundAtUtc <= nowUtc))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<RefundProgress> GetProgressAsync(Guid campaignId, int maxAttempts, CancellationToken cancellationToken)
    {
        var remaining = await _dbContext.Pledges
            .CountAsync(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded, cancellationToken);
        var exhausted = await _dbContext.Pledges
            .CountAsync(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded && x.RefundFailures >= maxAttempts, cancellationToken);

        return new RefundProgress(remaining, exhausted);
    }

    public async Task AddAttemptAsync(RefundAttempt attempt, CancellationToken cancellationToken)
        => await _dbContext.RefundAttempts.AddAsync(attempt, cancellationToken);
}
