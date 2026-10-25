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

    public async Task<IReadOnlyList<ChargedPledge>> GetChargedPledgesAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.Pledges
            .Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded)
            .Select(x => new ChargedPledge(x.Id, x.Money.Amount))
            .ToListAsync(cancellationToken);

    public async Task AddLedgerEntriesAsync(IEnumerable<PayoutLedgerEntry> entries, CancellationToken cancellationToken)
        => await _dbContext.PayoutLedgerEntries.AddRangeAsync(entries, cancellationToken);

    public async Task<IReadOnlyList<RefundedAfterPayout>> GetRefundedAfterPayoutAsync(int limit, CancellationToken cancellationToken)
    {
        var ledger = _dbContext.PayoutLedgerEntries;

        return await (
            from charge in ledger
            where charge.Kind == PayoutLedgerKind.Charge
            join pledge in _dbContext.Pledges on charge.PledgeId equals pledge.Id
            where pledge.Status == PledgeStatus.Refunded
                  && !ledger.Any(x => x.PayoutId == charge.PayoutId && x.PledgeId == charge.PledgeId && x.Kind == PayoutLedgerKind.ChargeRefunded)
            select new RefundedAfterPayout(
                charge.PayoutId,
                pledge.Id,
                charge.Amount,
                -(ledger
                    .Where(x => x.PayoutId == charge.PayoutId && x.PledgeId == charge.PledgeId && x.Kind == PayoutLedgerKind.PlatformFee)
                    .Sum(x => (decimal?)x.Amount) ?? 0m),
                charge.Currency))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<Payout?> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
        => _dbContext.Payouts.AsNoTracking().FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);

    public async Task<IReadOnlyList<PayoutLedgerEntry>> GetLedgerAsync(Guid payoutId, CancellationToken cancellationToken)
        => await _dbContext.PayoutLedgerEntries.AsNoTracking()
            .Where(x => x.PayoutId == payoutId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Kind)
            .ToListAsync(cancellationToken);
}
