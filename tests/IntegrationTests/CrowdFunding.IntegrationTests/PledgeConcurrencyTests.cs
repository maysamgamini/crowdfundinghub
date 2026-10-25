using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Pledges.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Verifies the xmin concurrency token added to PledgeConfiguration (QA ticket 008):
/// a payment confirmation and a payment failure racing on the same Pending pledge must not
/// silently overwrite each other — the loser must fail with a detectable conflict instead.
/// </summary>
[Collection(nameof(PledgesPostgresCollection))]
public sealed class PledgeConcurrencyTests
{
    private readonly PledgesPostgresFixture _fixture;

    public PledgeConcurrencyTests(PledgesPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfirmAndFail_ShouldNotBothSucceed_WhenRacingOnTheSamePendingPledge()
    {
        var pledge = Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", DateTime.UtcNow);

        await using (var seedContext = _fixture.CreateDbContext())
        {
            await seedContext.Pledges.AddAsync(pledge);
            await seedContext.SaveChangesAsync();
        }

        // Two independent DbContexts both load the same Pending row before either writes,
        // mirroring a confirmation webhook and a timeout/cancellation racing each other.
        await using var confirmContext = _fixture.CreateDbContext();
        await using var failContext = _fixture.CreateDbContext();

        var confirmSidePledge = await confirmContext.Pledges.SingleAsync(x => x.Id == pledge.Id);
        var failSidePledge = await failContext.Pledges.SingleAsync(x => x.Id == pledge.Id);

        var confirmExecutor = new PledgeTransactionExecutor(confirmContext);
        var failExecutor = new PledgeTransactionExecutor(failContext);

        await confirmExecutor.ExecuteAsync(async ct =>
        {
            confirmSidePledge.ConfirmPayment("payment-ref-123", DateTime.UtcNow);
            await new PledgeRepository(confirmContext).UpdateAsync(confirmSidePledge, ct);
            return 0;
        }, CancellationToken.None);

        var failAction = async () => await failExecutor.ExecuteAsync(async ct =>
        {
            failSidePledge.FailPayment("card declined", DateTime.UtcNow);
            await new PledgeRepository(failContext).UpdateAsync(failSidePledge, ct);
            return 0;
        }, CancellationToken.None);

        // The loser must fail loudly instead of overwriting the winner's committed state.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(failAction);

        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Pledges.SingleAsync(x => x.Id == pledge.Id);
        Assert.Equal(PledgeStatus.Succeeded, persisted.Status);
    }
}
