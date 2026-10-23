using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

/// <summary>
/// An idempotency key stops a retry of the same request; it cannot stop a backer who opens two tabs and
/// pledges twice with two keys. One live pledge per backer per campaign does. These tests pin down the rule
/// at the handler; the unique index that settles simultaneous requests is covered end to end.
/// </summary>
public sealed class OneLivePledgeTests
{
    private static readonly DateTime Now = new(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

    private static MakeContributionCommandHandler HandlerFor(TestCurrentUser user, FakeContributionRepository repository, IContributionTransactionExecutor? executor = null)
        => new(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user,
            new FakeContributionDateTimeProvider(Now),
            repository,
            executor ?? new FakeContributionTransactionExecutor());

    private static TestCurrentUser Backer() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsContribute] };

    [Fact]
    public async Task ASecondPledgeOnTheSameCampaign_WithADifferentKey_IsRefused()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var live = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "first");

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            HandlerFor(user, new FakeContributionRepository(live))
                .Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "second"), CancellationToken.None));

        Assert.Contains("already have a pledge", exception.Message);
    }

    [Fact]
    public async Task ASucceededPledgeAlsoCountsAsLive()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var live = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now);
        live.ConfirmPayment("ref", Now);

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            HandlerFor(user, new FakeContributionRepository(live))
                .Handle(new MakeContributionCommand(campaignId, 50m, "USD"), CancellationToken.None));
    }

    [Fact]
    public async Task AfterTheEarlierPledgeFailed_TheBackerCanPledgeAgain()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var failed = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now);
        failed.FailPayment("card declined", Now);

        var result = await HandlerFor(user, new FakeContributionRepository(failed))
            .Handle(new MakeContributionCommand(campaignId, 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.NotEqual(failed.Id, result.ContributionId);
    }

    [Fact]
    public async Task ABackersPledgeOnAnotherCampaign_DoesNotBlockThisOne()
    {
        var user = Backer();
        var elsewhere = Contribution.Create(Guid.NewGuid(), user.UserId, 100m, "USD", Now);

        var result = await HandlerFor(user, new FakeContributionRepository(elsewhere))
            .Handle(new MakeContributionCommand(Guid.NewGuid(), 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task AnotherBackersPledge_DoesNotBlockThisBacker()
    {
        var campaignId = Guid.NewGuid();
        var someoneElses = Contribution.Create(campaignId, Guid.NewGuid(), 100m, "USD", Now);

        var result = await HandlerFor(Backer(), new FakeContributionRepository(someoneElses))
            .Handle(new MakeContributionCommand(campaignId, 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task ARetryWithTheSameKey_StillReturnsTheFirstPledge_NotAConflict()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var first = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "same");

        var result = await HandlerFor(user, new FakeContributionRepository(first))
            .Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "same"), CancellationToken.None);

        Assert.True(result.IsReplay);
        Assert.Equal(first.Id, result.ContributionId);
    }

    private sealed class LosingTheRaceExecutor : IContributionTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
            => throw new AlreadyBackingException(new InvalidOperationException("unique violation"));

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
            => throw new AlreadyBackingException(new InvalidOperationException("unique violation"));
    }

    [Fact]
    public async Task LosingTheInsertRace_IsReportedAsTheSameConflict()
    {
        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            HandlerFor(Backer(), new FakeContributionRepository(), new LosingTheRaceExecutor())
                .Handle(new MakeContributionCommand(Guid.NewGuid(), 100m, "USD"), CancellationToken.None));
    }
}
