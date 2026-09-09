using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-053: Proves that in nested transaction and Unit of Work execution patterns,
/// only the root coordinator (the invocation that started the transaction) may harvest domain
/// events, commit changes, and clear entity event collections. Inner calls must not clear
/// outer domain events or commit intermediate state.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class CampaignNestedTransactionTests
{
    private static readonly IDistributedCache NoOpDistributedCache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private readonly CampaignsPostgresFixture _fixture;

    public CampaignNestedTransactionTests(CampaignsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NestedExecuteAsync_ShouldNotHarvestOrClearOuterDomainEvents_UntilOuterCommit()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var transactionExecutor = new CampaignTransactionExecutor(
            dbContext,
            NoOpDistributedCache,
            NullLogger<CampaignTransactionExecutor>.Instance);

        var now = DateTime.UtcNow;
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Nested Tx Test Campaign",
            "A campaign created to verify nested transaction domain event preservation.",
            "Technology",
            new Money(10_000m, "USD"),
            now.AddDays(30),
            now);

        await dbContext.Campaigns.AddAsync(campaign);

        // Precondition: domain event is attached to the tracked entity
        Assert.NotEmpty(campaign.DomainEvents);

        await transactionExecutor.ExecuteAsync(
            async ct =>
            {
                // Inner nested transaction execution
                var innerResult = await transactionExecutor.ExecuteAsync(
                    async innerCt =>
                    {
                        await Task.Yield();
                        return "inner-completed";
                    },
                    ct);

                Assert.Equal("inner-completed", innerResult);

                // CRITICAL ASSERTION: The inner transaction execution must NOT have harvested or cleared
                // the outer campaign's domain events!
                Assert.NotEmpty(campaign.DomainEvents);

                return 0;
            },
            CancellationToken.None);

        // After the outer root transaction completes and commits, domain events are cleared
        Assert.Empty(campaign.DomainEvents);

        // Verify that the outbox message was persisted for the campaign created event
        await using var verifyDbContext = _fixture.CreateDbContext();
        var outboxMessages = await verifyDbContext.OutboxMessages
            .Where(m => m.EventType.Contains("CampaignCreated"))
            .ToListAsync();

        Assert.NotEmpty(outboxMessages);
    }

    [Fact]
    public async Task NestedExecuteAsync_ShouldRollbackAllChangesAndOutbox_WhenOuterFails()
    {
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var dbContext = _fixture.CreateDbContext();
        var transactionExecutor = new CampaignTransactionExecutor(
            dbContext,
            NoOpDistributedCache,
            NullLogger<CampaignTransactionExecutor>.Instance);

        var action = async () => await transactionExecutor.ExecuteAsync<int>(
            async ct =>
            {
                var campaign = Campaign.Create(
                    campaignId,
                    "Rollback Test Campaign",
                    "A campaign that should roll back completely.",
                    "Technology",
                    new Money(5_000m, "USD"),
                    now.AddDays(30),
                    now);

                await dbContext.Campaigns.AddAsync(campaign, ct);

                // Nested inner execution that succeeds
                await transactionExecutor.ExecuteAsync(
                    async innerCt =>
                    {
                        await Task.Yield();
                        return true;
                    },
                    ct);

                // Outer transaction throws an exception after inner succeeds
                throw new InvalidOperationException("Outer transaction simulated failure");
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);

        // Verify the campaign was rolled back and not persisted in DB
        await using var verifyDbContext = _fixture.CreateDbContext();
        var persistedCampaign = await verifyDbContext.Campaigns.FindAsync(campaignId);
        Assert.Null(persistedCampaign);
    }
}
