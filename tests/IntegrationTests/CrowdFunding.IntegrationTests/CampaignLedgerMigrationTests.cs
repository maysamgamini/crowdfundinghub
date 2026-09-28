using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The AppendOnlyCampaignLedger migration on a database holding pre-ledger data: afterwards every
/// campaign's raised_amount equals the sum of its ledger entries.
/// </summary>
public sealed class CampaignLedgerMigrationTests : IAsyncLifetime
{
    private const string MigrationBeforeLedger = "20260909154404_AddRewardTierReservations";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Upgrade_ShouldBackfillPledges_AndAnOpeningBalance_SoTheSnapshotEqualsTheLedgerSum()
    {
        await using var dbContext = new CampaignsDbContext(
            new DbContextOptionsBuilder<CampaignsDbContext>().UseNpgsql(_container.GetConnectionString()).Options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeLedger);

        // Legacy state: campaign A has two deduped credits (150) but raised_amount 200 (50 credited
        // before the dedupe table existed); campaign B has none and raised_amount 0.
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaigns ("Id", "OwnerId", "Title", "Story", "Category", "DeadlineUtc", "Status", goal_amount, goal_currency, raised_amount, raised_currency, "CreatedAtUtc")
             VALUES ({campaignA}, {Guid.NewGuid()}, 'A', 'A story that is long enough.', 'Tech', now() + interval '30 days', 1, 1000, 'USD', 200, 'USD', now()),
                    ({campaignB}, {Guid.NewGuid()}, 'B', 'A story that is long enough.', 'Tech', now() + interval '30 days', 1, 1000, 'USD', 0, 'USD', now());
             INSERT INTO campaign_contributions_ledger ("Id", "CampaignId", "ContributionId", "Amount", "Currency", "RecordedAtUtc")
             VALUES ({Guid.NewGuid()}, {campaignA}, {Guid.NewGuid()}, 100, 'USD', now()),
                    ({Guid.NewGuid()}, {campaignA}, {Guid.NewGuid()}, 50, 'USD', now());
             """);

        await migrator.MigrateAsync();

        var rows = await dbContext.Database.SqlQuery<SnapshotRow>(
                $"""
                 SELECT c."Id" AS "CampaignId", c.raised_amount AS "RaisedAmount",
                        COALESCE((SELECT SUM(e."Amount") FROM campaigns.campaign_ledger_entries e WHERE e."CampaignId" = c."Id"), 0) AS "LedgerSum",
                        (SELECT COUNT(*) FROM campaigns.campaign_ledger_entries e WHERE e."CampaignId" = c."Id" AND e."Kind" = 2)::int AS "OpeningBalances"
                 FROM campaigns.campaigns c
                 """)
            .ToListAsync();

        Assert.All(rows, row => Assert.Equal(row.RaisedAmount, row.LedgerSum));
        Assert.Equal(1, rows.Single(row => row.CampaignId == campaignA).OpeningBalances);
        Assert.Equal(0, rows.Single(row => row.CampaignId == campaignB).OpeningBalances);
    }

    private sealed record SnapshotRow(Guid CampaignId, decimal RaisedAmount, decimal LedgerSum, int OpeningBalances);
}
