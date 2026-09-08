using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements the contribution ledger using a PostgreSQL <c>ON CONFLICT DO NOTHING</c> insert
/// against the unique index on <c>contribution_id</c>, so redelivery of the same confirmation
/// event is a cheap, exception-free idempotent no-op instead of relying on catching a unique-
/// violation exception.
/// </summary>
public sealed class ContributionLedger : IContributionLedger
{
    private readonly CampaignsDbContext _dbContext;

    public ContributionLedger(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryRecordAsync(
        Guid campaignId,
        Guid contributionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        // Column names are double-quoted to match EF's default PascalCase mapping for this
        // entity (no HasColumnName overrides configured); the table name and the unique index
        // name on ContributionId, by contrast, are explicitly snake_case in
        // ContributionLedgerEntryConfiguration.
        var rowsInserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaign_contributions_ledger ("Id", "CampaignId", "ContributionId", "Amount", "Currency", "RecordedAtUtc")
             VALUES ({Guid.NewGuid()}, {campaignId}, {contributionId}, {amount}, {currency}, {DateTime.UtcNow})
             ON CONFLICT ("ContributionId") DO NOTHING
             """,
            cancellationToken);

        return rowsInserted > 0;
    }
}
