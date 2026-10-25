using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

public sealed class PledgeIdempotencyTests
{
    private static readonly DateTime Now = new(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

    private static (MakePledgeCommandHandler Handler, FakePledgeRepository Repository, FakePledgeTransactionExecutor Executor, TestCurrentUser User) Build(
        FakePledgeRepository? repository = null)
    {
        repository ??= new FakePledgeRepository();
        var executor = new FakePledgeTransactionExecutor();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsPledge] };
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user,
            new FakePledgeDateTimeProvider(Now),
            repository,
            executor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());
        return (handler, repository, executor, user);
    }

    [Fact]
    public void Create_ShouldKeepTheIdempotencyKey()
    {
        var pledge = Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: "key-1");

        Assert.Equal("key-1", pledge.IdempotencyKey);
    }

    [Theory]
    [InlineData("")]
    public void Create_ShouldRejectAnEmptyKey(string key)
    {
        Assert.Throws<ArgumentException>(() => Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: key));
    }

    [Fact]
    public void Create_ShouldRejectAKeyLongerThanTheLimit()
    {
        var tooLong = new string('k', Pledge.MaxIdempotencyKeyLength + 1);

        Assert.Throws<ArgumentException>(() => Pledge.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: tooLong));
    }

    [Theory]
    [InlineData("abc-123_XYZ.9", true)]
    [InlineData("has space", false)]
    [InlineData("tab\there", false)]
    [InlineData("caf\u00e9", false)]
    public void Validator_ShouldAcceptOnlyPrintableAsciiWithoutSpaces(string key, bool valid)
    {
        var result = new MakePledgeCommandValidator()
            .Validate(new MakePledgeCommand(Guid.NewGuid(), 10m, "USD", IdempotencyKey: key));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Validator_ShouldAcceptARequestWithNoKey()
    {
        var result = new MakePledgeCommandValidator()
            .Validate(new MakePledgeCommand(Guid.NewGuid(), 10m, "USD"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_WithAKey_ShouldStoreItOnTheNewPledge()
    {
        var (handler, repository, _, _) = Build();

        var result = await handler.Handle(
            new MakePledgeCommand(Guid.NewGuid(), 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Equal("k1", repository.SavedPledge!.IdempotencyKey);
    }

    [Fact]
    public async Task Handle_RetryWithTheSameKeyAndRequest_ShouldReturnTheFirstPledge()
    {
        var campaignId = Guid.NewGuid();
        var (first, repository, _, user) = Build();
        var created = await first.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        // Same backer, same repository: the retry must find the first pledge and add nothing.
        var executor = new FakePledgeTransactionExecutor();
        var retryHandler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakePledgeDateTimeProvider(Now), repository, executor, new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var replay = await retryHandler.Handle(new MakePledgeCommand(campaignId, 100m, "usd", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Equal(created.PledgeId, replay.PledgeId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task Handle_RetryAfterTheCampaignClosed_ShouldStillReturnTheFirstPledge()
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsPledge] };
        var earlier = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1");
        var repository = new FakePledgeRepository(earlier);

        // The campaign is no longer active, which would reject a brand new pledge.
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            user, new FakePledgeDateTimeProvider(Now), repository, new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var replay = await handler.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Equal(earlier.Id, replay.PledgeId);
    }

    [Theory]
    [InlineData(250, "USD")]
    [InlineData(100, "EUR")]
    public async Task Handle_SameKeyDifferentRequest_ShouldBeRejectedAsAConflict(int amount, string currency)
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsPledge] };
        var repository = new FakePledgeRepository(Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1"));
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakePledgeDateTimeProvider(Now), repository, new FakePledgeTransactionExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            handler.Handle(new MakePledgeCommand(campaignId, amount, currency, IdempotencyKey: "k1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameKeyFromAnotherBacker_ShouldCreateAnotherPledge()
    {
        var campaignId = Guid.NewGuid();
        var otherBackersPledge = Pledge.Create(campaignId, Guid.NewGuid(), 100m, "USD", Now, idempotencyKey: "k1");
        var (handler, repository, _, _) = Build(new FakePledgeRepository(otherBackersPledge));

        var result = await handler.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.NotEqual(otherBackersPledge.Id, result.PledgeId);
        Assert.NotNull(repository.SavedPledge);
    }

    [Fact]
    public async Task Handle_WithoutAKey_IsNotDeduplicatedByKey_TheOneLivePledgeRuleStopsTheSecond()
    {
        var (handler, repository, executor, _) = Build();
        var command = new MakePledgeCommand(Guid.NewGuid(), 100m, "USD");

        await handler.Handle(command, CancellationToken.None);
        // The first pledge is now live for this backer on this campaign, so a second request with no key
        // is refused by the one-live-pledge rule, not treated as a retry.
        await Assert.ThrowsAsync<ResourceConflictException>(() => handler.Handle(command, CancellationToken.None));

        Assert.NotNull(repository.SavedPledge);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task Handle_LosingTheInsertRace_ShouldAnswerWithTheWinningPledge()
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsPledge] };
        var winner = Pledge.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1");
        var repository = new RaceLosingRepository(winner);
        var handler = new MakePledgeCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakePledgeDateTimeProvider(Now), repository, new RaceLosingExecutor(), new CrowdFunding.Modules.Pledges.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var result = await handler.Handle(new MakePledgeCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(result.IsReplay);
        Assert.Equal(winner.Id, result.PledgeId);
    }

    /// <summary>Reports no pledge on the first lookup (the race has not happened yet) and the winner on the second.</summary>
    private sealed class RaceLosingRepository(Pledge winner) : FakePledgeRepositoryBase
    {
        private int _lookups;

        public override Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult<Pledge?>(++_lookups == 1 ? null : winner);
    }

    /// <summary>Behaves like the real executor when the unique index rejects the insert.</summary>
    private sealed class RaceLosingExecutor : IPledgeTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
            => throw new IdempotencyKeyAlreadyUsedException(new InvalidOperationException("unique violation"));

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
            => throw new IdempotencyKeyAlreadyUsedException(new InvalidOperationException("unique violation"));
    }

    private abstract class FakePledgeRepositoryBase : IPledgeRepository
    {
        public Task AddActivationAsync(PledgeActivation activation, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(Pledge pledge, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Pledge?> GetByIdAsync(Guid pledgeId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public Task<Pledge?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public abstract Task<Pledge?> GetByIdempotencyKeyAsync(Guid backerId, string idempotencyKey, CancellationToken cancellationToken);
        public Task<Pledge?> GetLivePledgeAsync(Guid campaignId, Guid backerId, CancellationToken cancellationToken) => Task.FromResult<Pledge?>(null);
        public Task UpdateAsync(Pledge pledge, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Pledge>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Pledge>>([]);
        public Task<IReadOnlyList<Pledge>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Pledge>>([]);
    }
}
