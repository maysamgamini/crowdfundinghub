using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

/// <summary>
/// The campaign's append-only ledger. Entries are only ever inserted (a database trigger refuses updates
/// and deletes), a pledge is positive and a refund is a negative compensating entry, and each
/// (pledge, kind) pair can be written once, so a redelivered event cannot count twice.
/// </summary>
public sealed class PledgeLedger : IPledgeLedger
{
    private readonly CampaignsDbContext _dbContext;

    public PledgeLedger(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> TryRecordAsync(
        Guid campaignId,
        Guid pledgeId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
        => InsertAsync(campaignId, pledgeId, LedgerEntryKind.Pledge, amount, currency, cancellationToken);

    public Task<bool> TryRecordRefundAsync(
        Guid campaignId,
        Guid pledgeId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
        => InsertAsync(campaignId, pledgeId, LedgerEntryKind.Refund, -Math.Abs(amount), currency, cancellationToken);

    public Task<bool> HasPledgeAsync(Guid pledgeId, CancellationToken cancellationToken)
        => _dbContext.Set<PledgeLedgerEntry>()
            .AnyAsync(entry => entry.PledgeId == pledgeId && entry.Kind == LedgerEntryKind.Pledge, cancellationToken);

    public Task<bool> HasRefundAsync(Guid pledgeId, CancellationToken cancellationToken)
        => _dbContext.Set<PledgeLedgerEntry>()
            .AnyAsync(entry => entry.PledgeId == pledgeId && entry.Kind == LedgerEntryKind.Refund, cancellationToken);

    public async Task<decimal> SumAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.Set<PledgeLedgerEntry>()
            .Where(entry => entry.CampaignId == campaignId)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;

    private async Task<bool> InsertAsync(
        Guid campaignId,
        Guid pledgeId,
        LedgerEntryKind kind,
        decimal signedAmount,
        string currency,
        CancellationToken cancellationToken)
    {
        // Column names are double-quoted to match EF's default PascalCase mapping for this entity; the table
        // name and the unique index are explicitly snake_case in PledgeLedgerEntryConfiguration.
        var kindName = kind.ToString();
        var rowsInserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaigns.campaign_pledges_ledger ("Id", "CampaignId", "PledgeId", "Kind", "Amount", "Currency", "RecordedAtUtc")
             VALUES ({Guid.NewGuid()}, {campaignId}, {pledgeId}, {kindName}, {signedAmount}, {currency}, {DateTime.UtcNow})
             ON CONFLICT ("PledgeId", "Kind") DO NOTHING
             """,
            cancellationToken);

        return rowsInserted > 0;
    }
}
