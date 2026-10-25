using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
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

    private static MakePledgeCommandHandler HandlerFor(TestCurrentUser user, FakePledgeRepository repository, IPledgeTransactionExecutor? executor = null)
        => new(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user,
            new FakePledgeDateTimeProvider(Now),
            repository,
            executor ?? new FakePledgeTransactionExecutor(),
            new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(),
            new FakePaymentSettings());

    private static TestCurrentUser Backer() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsPledge] };

    [Fact]
    public async Task ASecondPledgeOnTheSameCampaign_WithADifferentKey_IsRefused()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var live = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "first");

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            HandlerFor(user, new FakePledgeRepository(live))
                .Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "second"), CancellationToken.None));

        Assert.Contains("already have a pledge", exception.Message);
    }

    [Fact]
    public async Task ASucceededPledgeAlsoCountsAsLive()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var live = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now);
        live.ConfirmPayment("ref", Now);

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            HandlerFor(user, new FakePledgeRepository(live))
                .Handle(new MakePledgeCommand(campaignId, 50m, "USD"), CancellationToken.None));
    }

    [Fact]
    public async Task AfterTheEarlierPledgeFailed_TheBackerCanPledgeAgain()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var failed = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now);
        failed.FailPayment("card declined", Now);

        var result = await HandlerFor(user, new FakePledgeRepository(failed))
            .Handle(new MakePledgeCommand(campaignId, 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.NotEqual(failed.Id, result.PledgeId);
    }

    [Fact]
    public async Task ABackersPledgeOnAnotherCampaign_DoesNotBlockThisOne()
    {
        var user = Backer();
        var elsewhere = Pledge.Create(Guid.NewGuid(), user.UserId, 100m, "USD", Now);

        var result = await HandlerFor(user, new FakePledgeRepository(elsewhere))
            .Handle(new MakePledgeCommand(Guid.NewGuid(), 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task AnotherBackersPledge_DoesNotBlockThisBacker()
    {
        var campaignId = Guid.NewGuid();
        var someoneElses = Pledge.Create(campaignId, Guid.NewGuid(), 100m, "USD", Now);

        var result = await HandlerFor(Backer(), new FakePledgeRepository(someoneElses))
            .Handle(new MakePledgeCommand(campaignId, 100m, "USD"), CancellationToken.None);

        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task ARetryWithTheSameKey_StillReturnsTheFirstPledge_NotAConflict()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var first = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "same");

        var result = await HandlerFor(user, new FakePledgeRepository(first))
            .Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "same"), CancellationToken.None);

        Assert.True(result.IsReplay);
        Assert.Equal(first.Id, result.PledgeId);
    }

    private sealed class LosingTheRaceExecutor : IPledgeTransactionExecutor
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
            HandlerFor(Backer(), new FakePledgeRepository(), new LosingTheRaceExecutor())
                .Handle(new MakePledgeCommand(Guid.NewGuid(), 100m, "USD"), CancellationToken.None));
    }

    /// <summary>The key lookup finds nothing (the other request has not committed yet), then the live-pledge lookup finds the winner.</summary>
    private sealed class WinnerAppearsLateRepository(Pledge winner) : IPledgeRepository
    {
        public Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(Pledge pledge, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(winner);
        public Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Pledge>>([]);
        public Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Pledge>>([]);
    }

    [Fact]
    public async Task ARetryThatSeesTheWinnersPledgeOnlyInTheLivePledgeCheck_StillGetsTheFirstPledge()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var winner = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "same");
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true), user, new FakePledgeDateTimeProvider(Now),
            new WinnerAppearsLateRepository(winner), new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var result = await handler.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "same"), CancellationToken.None);

        Assert.True(result.IsReplay);
        Assert.Equal(winner.Id, result.PledgeId);
    }

    [Fact]
    public async Task ADifferentKeyAgainstTheSameLivePledge_IsStillAConflict()
    {
        var campaignId = Guid.NewGuid();
        var user = Backer();
        var winner = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "first");
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true), user, new FakePledgeDateTimeProvider(Now),
            new WinnerAppearsLateRepository(winner), new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            handler.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "second"), CancellationToken.None));
    }
}
