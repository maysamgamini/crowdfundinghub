using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Caching;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL implementation of <see cref="ICampaignLedger"/>: two statements, both letting the
/// database decide.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>
/// The append is <c>INSERT … ON CONFLICT DO NOTHING</c> on the (ContributionId, Kind) unique key,
/// so a duplicate is rejected by the database rather than by a prior SELECT that a concurrent
/// transaction could race. A refund additionally requires, in the same statement, that a pledge
/// entry exists for the contribution.
/// </item>
/// <item>
/// The snapshot is <c>UPDATE campaigns SET raised_amount = SUM(entries)</c>: recomputed from the
/// ledger, never incremented, so it cannot drift from it. The caller's advisory lock guarantees
/// the SUM sees every committed entry.
/// </item>
/// </list>
/// Table names are schema-qualified (<c>campaigns.</c>, see <see cref="CampaignsDbContext.Schema"/>)
/// so the statements never depend on <c>search_path</c>; values are parameters.
/// </remarks>
public sealed class CampaignLedger : ICampaignLedger
{
    private readonly CampaignsDbContext _dbContext;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignLedger"/> class.
    /// </summary>
    /// <param name="dbContext">The Campaigns DbContext.</param>
    /// <param name="transactionExecutor">Used to queue the campaign's cache invalidation until commit.</param>
    public CampaignLedger(CampaignsDbContext dbContext, ICampaignTransactionExecutor transactionExecutor)
    {
        _dbContext = dbContext;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<CampaignLedgerAppendResult> AppendAsync(CampaignLedgerEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"{nameof(CampaignLedger)}.{nameof(AppendAsync)} must run inside the campaign's transaction (ICampaignTransactionExecutor with its advisory lock).");
        }

        var appended = entry.Kind == CampaignLedgerEntryKind.Refund
            ? await AppendRefundAsync(entry, cancellationToken)
            : await AppendPledgeAsync(entry, cancellationToken);

        var total = await RecomputeRaisedAmountAsync(entry.CampaignId, cancellationToken);

        if (appended)
        {
            _transactionExecutor.EnqueueCacheInvalidation(CampaignCacheKeys.Details(entry.CampaignId));
        }

        return new CampaignLedgerAppendResult(appended, total);
    }

    private async Task<bool> AppendPledgeAsync(CampaignLedgerEntry entry, CancellationToken cancellationToken)
    {
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaigns.campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
             VALUES ({entry.Id}, {entry.CampaignId}, {entry.ContributionId}, {(int)entry.Kind}, {entry.Amount}, {entry.Currency}, {entry.RecordedAtUtc})
             ON CONFLICT ("ContributionId", "Kind") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    private async Task<bool> AppendRefundAsync(CampaignLedgerEntry entry, CancellationToken cancellationToken)
    {
        // A refund compensates a credited pledge: no pledge entry for the contribution, no refund
        // entry. Checked in the same statement as the insert, not in a separate SELECT.
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaigns.campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
             SELECT {entry.Id}, {entry.CampaignId}, {entry.ContributionId}, {(int)entry.Kind}, {entry.Amount}, {entry.Currency}, {entry.RecordedAtUtc}
             WHERE EXISTS (
                 SELECT 1 FROM campaigns.campaign_ledger_entries
                 WHERE "CampaignId" = {entry.CampaignId}
                   AND "ContributionId" = {entry.ContributionId}
                   AND "Kind" = {(int)CampaignLedgerEntryKind.Pledge})
             ON CONFLICT ("ContributionId", "Kind") DO NOTHING
             """,
            cancellationToken);

        return inserted == 1;
    }

    private async Task<decimal> RecomputeRaisedAmountAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var snapshot = (await _dbContext.Database.SqlQuery<SnapshotRow>(
                $"""
                 UPDATE campaigns.campaigns
                 SET raised_amount = (SELECT COALESCE(SUM("Amount"), 0) FROM campaigns.campaign_ledger_entries WHERE "CampaignId" = {campaignId})
                 WHERE "Id" = {campaignId}
                 RETURNING raised_amount AS "RaisedAmount", xmin::text::bigint AS "Xmin"
                 """)
            .ToListAsync(cancellationToken)).Single();

        SyncTrackedCampaign(campaignId, snapshot);
        return snapshot.RaisedAmount;
    }

    /// <summary>
    /// The UPDATE above bypassed the change tracker. If the caller holds the campaign as a tracked
    /// entity, copy the database's new raised amount and row version into it as both current and
    /// original values, so the aggregate reflects the ledger and EF still sees it as unchanged
    /// (otherwise a later SaveChanges would send the stale xmin and fail its concurrency check).
    /// </summary>
    private void SyncTrackedCampaign(Guid campaignId, SnapshotRow snapshot)
    {
        var entry = _dbContext.ChangeTracker.Entries<Campaign>().SingleOrDefault(tracked => tracked.Entity.Id == campaignId);
        if (entry is null)
        {
            return;
        }

        // xid is an unsigned 32-bit value; read through bigint because unmapped SQL queries
        // have no xid-to-uint mapping.
        var xmin = checked((uint)snapshot.Xmin);
        var rowVersion = entry.Property("xmin");
        rowVersion.CurrentValue = xmin;
        rowVersion.OriginalValue = xmin;

        var raisedAmount = entry.Reference(campaign => campaign.RaisedAmount).TargetEntry!.Property(money => money.Amount);
        raisedAmount.CurrentValue = snapshot.RaisedAmount;
        raisedAmount.OriginalValue = snapshot.RaisedAmount;
    }

    private sealed record SnapshotRow(decimal RaisedAmount, long Xmin);
}
