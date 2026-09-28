using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// <see cref="OutboxCommitSignalInterceptor{TDbContext}"/> against real PostgreSQL: the outbox
/// processor is woken only once outbox rows are committed, never for a rolled-back transaction
/// (these tests use a private signal instance so the host's own processor is not involved).
/// Also covers lease recovery of rows a crashed worker left in Processing.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OutboxSignalE2ETests
{
    private static readonly TimeSpan NoSignalWindow = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    private readonly CrowdFundingApiFactory _factory;

    public OutboxSignalE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OutboxWriteInsideATransaction_ShouldSignalOnlyAfterCommit()
    {
        var signal = new OutboxSignal<CampaignsDbContext>();
        await using var dbContext = CreateContext(signal);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            var message = AddOutboxMessage(dbContext);
            await dbContext.SaveChangesAsync();

            Assert.False(await signal.WaitAsync(NoSignalWindow, CancellationToken.None));

            await transaction.CommitAsync();
            Assert.True(await signal.WaitAsync(SignalTimeout, CancellationToken.None));

            await RemoveAsync(dbContext, message);
        }
    }

    [Fact]
    public async Task OutboxWriteWithoutAnExplicitTransaction_ShouldSignalAfterSaveChanges()
    {
        var signal = new OutboxSignal<CampaignsDbContext>();
        await using var dbContext = CreateContext(signal);

        var message = AddOutboxMessage(dbContext);
        await dbContext.SaveChangesAsync();

        Assert.True(await signal.WaitAsync(SignalTimeout, CancellationToken.None));

        await RemoveAsync(dbContext, message);
    }

    [Fact]
    public async Task RolledBackOutboxWrite_ShouldNotSignal()
    {
        var signal = new OutboxSignal<CampaignsDbContext>();
        await using var dbContext = CreateContext(signal);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            AddOutboxMessage(dbContext);
            await dbContext.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.False(await signal.WaitAsync(NoSignalWindow, CancellationToken.None));
    }

    [Fact]
    public async Task RowAbandonedInProcessing_ShouldBeReclaimedOnceItsLeaseExpires()
    {
        // Simulates a worker that claimed a row and crashed before publishing it: the row is left
        // in Processing with a lease that has already run out. Written in one transaction so the
        // host's processor never sees it as an ordinary Pending row first.
        await using var dbContext = CreateContext(new OutboxSignal<CampaignsDbContext>());
        OutboxMessage message;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            message = AddOutboxMessage(dbContext);
            await dbContext.SaveChangesAsync();
            await dbContext.OutboxMessages
                .Where(row => row.Id == message.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, OutboxMessageStatus.Processing)
                    .SetProperty(row => row.LockedBy, "crashed-worker")
                    .SetProperty(row => row.LockedUntilUtc, DateTime.UtcNow.AddMinutes(-1)));
            await transaction.CommitAsync();
        }

        await _factory.ProcessOutboxMessagesAsync();

        // Reclaimed and processed: the probe type is unregistered, so processing dead-letters it.
        Assert.False(await dbContext.OutboxMessages.AnyAsync(row => row.Id == message.Id));
        Assert.True(await dbContext.DeadLetterEvents.AnyAsync(row => row.SourceEventId == message.Id));
    }

    private CampaignsDbContext CreateContext(OutboxSignal<CampaignsDbContext> signal)
    {
        using var scope = _factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>().Database.GetConnectionString();

        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new OutboxCommitSignalInterceptor<CampaignsDbContext>(signal))
            .Options;

        return new CampaignsDbContext(options);
    }

    /// <summary>
    /// The payload type is deliberately unregistered: should the host's fallback sweep claim the
    /// row before the test removes it, it is dead-lettered instead of reaching any handler.
    /// </summary>
    private static OutboxMessage AddOutboxMessage(CampaignsDbContext dbContext)
    {
        var message = OutboxMessage.Create(new OutboxSignalProbe(Guid.NewGuid()), DateTime.UtcNow);
        dbContext.OutboxMessages.Add(message);
        return message;
    }

    private static async Task RemoveAsync(CampaignsDbContext dbContext, OutboxMessage message)
    {
        await dbContext.OutboxMessages.Where(row => row.Id == message.Id).ExecuteDeleteAsync();
    }

    private sealed record OutboxSignalProbe(Guid Id);
}
