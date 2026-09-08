using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Contributions.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Verifies the xmin concurrency token added to ContributionConfiguration (QA ticket 008):
/// a payment confirmation and a payment failure racing on the same Pending contribution must not
/// silently overwrite each other — the loser must fail with a detectable conflict instead.
/// </summary>
[Collection(nameof(ContributionsPostgresCollection))]
public sealed class ContributionConcurrencyTests
{
    private readonly ContributionsPostgresFixture _fixture;

    public ContributionConcurrencyTests(ContributionsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfirmAndFail_ShouldNotBothSucceed_WhenRacingOnTheSamePendingContribution()
    {
        var contribution = Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", DateTime.UtcNow);

        await using (var seedContext = _fixture.CreateDbContext())
        {
            await seedContext.Contributions.AddAsync(contribution);
            await seedContext.SaveChangesAsync();
        }

        // Two independent DbContexts both load the same Pending row before either writes,
        // mirroring a confirmation webhook and a timeout/cancellation racing each other.
        await using var confirmContext = _fixture.CreateDbContext();
        await using var failContext = _fixture.CreateDbContext();

        var confirmSideContribution = await confirmContext.Contributions.SingleAsync(x => x.Id == contribution.Id);
        var failSideContribution = await failContext.Contributions.SingleAsync(x => x.Id == contribution.Id);

        var confirmExecutor = new ContributionTransactionExecutor(confirmContext);
        var failExecutor = new ContributionTransactionExecutor(failContext);

        await confirmExecutor.ExecuteAsync(async ct =>
        {
            confirmSideContribution.ConfirmPayment("payment-ref-123", DateTime.UtcNow);
            await new ContributionRepository(confirmContext).UpdateAsync(confirmSideContribution, ct);
            return 0;
        }, CancellationToken.None);

        var failAction = async () => await failExecutor.ExecuteAsync(async ct =>
        {
            failSideContribution.FailPayment("card declined", DateTime.UtcNow);
            await new ContributionRepository(failContext).UpdateAsync(failSideContribution, ct);
            return 0;
        }, CancellationToken.None);

        // The loser must fail loudly instead of overwriting the winner's committed state.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(failAction);

        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Contributions.SingleAsync(x => x.Id == contribution.Id);
        Assert.Equal(ContributionStatus.Succeeded, persisted.Status);
    }
}
