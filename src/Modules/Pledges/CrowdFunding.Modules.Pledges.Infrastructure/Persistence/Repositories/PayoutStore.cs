using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

public sealed class PayoutStore : IPayoutStore
{
    private static readonly PledgeStatus[] StillOnTheWay = [PledgeStatus.Active, PledgeStatus.ToCharge, PledgeStatus.Charging];

    private readonly PledgesDbContext _dbContext;

    public PayoutStore(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
        => _dbContext.Payouts.AnyAsync(x => x.CampaignId == campaignId, cancellationToken);

    public async Task AddAsync(Payout payout, CancellationToken cancellationToken)
        => await _dbContext.Payouts.AddAsync(payout, cancellationToken);

    public async Task<IReadOnlyList<Payout>> GetDueAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken)
        => await _dbContext.Payouts
            .AsTracking()
            .Where(x => (x.Status == PayoutStatus.Pending || x.Status == PayoutStatus.Ready)
                        && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= nowUtc))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(Payout payout, CancellationToken cancellationToken)
    {
        _dbContext.Payouts.Update(payout);
        return Task.CompletedTask;
    }

    public Task<int> CountUnsettledPledgesAsync(Guid campaignId, CancellationToken cancellationToken)
        => _dbContext.Pledges.CountAsync(x => x.CampaignId == campaignId && StillOnTheWay.Contains(x.Status), cancellationToken);

    public async Task<decimal> SumChargedAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.Pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Money.Amount, cancellationToken) ?? 0m;
}
