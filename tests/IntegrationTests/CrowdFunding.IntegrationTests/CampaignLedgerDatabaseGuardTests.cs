using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The ledger is a record. These tests go around the application with plain SQL and watch the database enforce
/// its shape: a pledge is positive and a refund negative, each contribution appears once per kind, and nothing
/// can be changed or deleted afterwards. The only way to undo an entry is another entry.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class CampaignLedgerDatabaseGuardTests
{
    private readonly CrowdFundingApiFactory _factory;

    public CampaignLedgerDatabaseGuardTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedCampaignAsync()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(), "Ledger guard campaign", "A story that is definitely longer than twenty characters, describing it in full.",
            "Community", new Money(1000m, "USD"), DateTime.UtcNow.AddDays(30), DateTime.UtcNow);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        db.Add(campaign);
        await db.SaveChangesAsync();
        return campaign.Id;
    }

    private async Task InsertAsync(Guid campaignId, Guid contributionId, string kind, decimal amount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO campaigns.campaign_contributions_ledger ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
             VALUES ({Guid.NewGuid()}, {campaignId}, {contributionId}, {kind}, {amount}, 'USD', {DateTime.UtcNow})
             """);
    }

    [Fact]
    public async Task APledgeWithANegativeAmount_IsRefusedByTheDatabase()
    {
        var campaignId = await SeedCampaignAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(campaignId, Guid.NewGuid(), "Pledge", -10m));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_campaign_contributions_ledger_sign_matches_kind", exception.ConstraintName);
    }

    [Fact]
    public async Task ARefundWithAPositiveAmount_IsRefusedByTheDatabase()
    {
        var campaignId = await SeedCampaignAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(campaignId, Guid.NewGuid(), "Refund", 10m));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task AZeroAmount_IsRefusedForEitherKind()
    {
        var campaignId = await SeedCampaignAsync();

        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(campaignId, Guid.NewGuid(), "Pledge", 0m));
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(campaignId, Guid.NewGuid(), "Refund", 0m));
    }

    [Fact]
    public async Task AnEntryCannotBeUpdated()
    {
        var campaignId = await SeedCampaignAsync();
        var contributionId = Guid.NewGuid();
        await InsertAsync(campaignId, contributionId, "Pledge", 50m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE campaigns.campaign_contributions_ledger SET "Amount" = 5000 WHERE "ContributionId" = {contributionId}"""));

        Assert.Contains("append-only", exception.MessageText);
    }

    [Fact]
    public async Task AnEntryCannotBeDeleted()
    {
        var campaignId = await SeedCampaignAsync();
        var contributionId = Guid.NewGuid();
        await InsertAsync(campaignId, contributionId, "Pledge", 50m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM campaigns.campaign_contributions_ledger WHERE "ContributionId" = {contributionId}"""));

        Assert.Contains("append-only", exception.MessageText);
    }

    [Fact]
    public async Task EachContributionAppearsOncePerKind_AndAPledgeAndItsRefundMayCoexist()
    {
        var campaignId = await SeedCampaignAsync();
        var contributionId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var ledger = new ContributionLedger(db);

        Assert.True(await ledger.TryRecordAsync(campaignId, contributionId, 80m, "USD", CancellationToken.None));
        Assert.False(await ledger.TryRecordAsync(campaignId, contributionId, 80m, "USD", CancellationToken.None));
        Assert.True(await ledger.TryRecordRefundAsync(campaignId, contributionId, 80m, "USD", CancellationToken.None));
        Assert.False(await ledger.TryRecordRefundAsync(campaignId, contributionId, 80m, "USD", CancellationToken.None));

        Assert.True(await ledger.HasPledgeAsync(contributionId, CancellationToken.None));
        Assert.True(await ledger.HasRefundAsync(contributionId, CancellationToken.None));
        Assert.Equal(0m, await ledger.SumAsync(campaignId, CancellationToken.None));
    }

    [Fact]
    public async Task TheSum_IsEveryPledgeMinusEveryRefund()
    {
        var campaignId = await SeedCampaignAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var ledger = new ContributionLedger(scope.ServiceProvider.GetRequiredService<CampaignsDbContext>());
        var refunded = Guid.NewGuid();

        await ledger.TryRecordAsync(campaignId, Guid.NewGuid(), 100m, "USD", CancellationToken.None);
        await ledger.TryRecordAsync(campaignId, refunded, 60m, "USD", CancellationToken.None);
        await ledger.TryRecordRefundAsync(campaignId, refunded, 60m, "USD", CancellationToken.None);
        await ledger.TryRecordAsync(campaignId, Guid.NewGuid(), 25m, "USD", CancellationToken.None);

        Assert.Equal(125m, await ledger.SumAsync(campaignId, CancellationToken.None));
    }
}
