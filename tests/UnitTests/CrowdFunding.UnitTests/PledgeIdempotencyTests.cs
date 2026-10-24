using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

public sealed class PledgeIdempotencyTests
{
    private static readonly DateTime Now = new(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

    private static (MakeContributionCommandHandler Handler, FakeContributionRepository Repository, FakeContributionTransactionExecutor Executor, TestCurrentUser User) Build(
        FakeContributionRepository? repository = null)
    {
        repository ??= new FakeContributionRepository();
        var executor = new FakeContributionTransactionExecutor();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsContribute] };
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user,
            new FakeContributionDateTimeProvider(Now),
            repository,
            executor, new CrowdFunding.Modules.Contributions.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());
        return (handler, repository, executor, user);
    }

    [Fact]
    public void Create_ShouldKeepTheIdempotencyKey()
    {
        var pledge = Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: "key-1");

        Assert.Equal("key-1", pledge.IdempotencyKey);
    }

    [Theory]
    [InlineData("")]
    public void Create_ShouldRejectAnEmptyKey(string key)
    {
        Assert.Throws<ArgumentException>(() => Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: key));
    }

    [Fact]
    public void Create_ShouldRejectAKeyLongerThanTheLimit()
    {
        var tooLong = new string('k', Contribution.MaxIdempotencyKeyLength + 1);

        Assert.Throws<ArgumentException>(() => Contribution.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Now, idempotencyKey: tooLong));
    }

    [Theory]
    [InlineData("abc-123_XYZ.9", true)]
    [InlineData("has space", false)]
    [InlineData("tab\there", false)]
    [InlineData("caf\u00e9", false)]
    public void Validator_ShouldAcceptOnlyPrintableAsciiWithoutSpaces(string key, bool valid)
    {
        var result = new MakeContributionCommandValidator()
            .Validate(new MakeContributionCommand(Guid.NewGuid(), 10m, "USD", IdempotencyKey: key));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Validator_ShouldAcceptARequestWithNoKey()
    {
        var result = new MakeContributionCommandValidator()
            .Validate(new MakeContributionCommand(Guid.NewGuid(), 10m, "USD"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_WithAKey_ShouldStoreItOnTheNewPledge()
    {
        var (handler, repository, _, _) = Build();

        var result = await handler.Handle(
            new MakeContributionCommand(Guid.NewGuid(), 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Equal("k1", repository.SavedContribution!.IdempotencyKey);
    }

    [Fact]
    public async Task Handle_RetryWithTheSameKeyAndRequest_ShouldReturnTheFirstPledge()
    {
        var campaignId = Guid.NewGuid();
        var (first, repository, _, user) = Build();
        var created = await first.Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        // Same backer, same repository: the retry must find the first pledge and add nothing.
        var executor = new FakeContributionTransactionExecutor();
        var retryHandler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakeContributionDateTimeProvider(Now), repository, executor, new CrowdFunding.Modules.Contributions.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var replay = await retryHandler.Handle(new MakeContributionCommand(campaignId, 100m, "usd", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Equal(created.ContributionId, replay.ContributionId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task Handle_RetryAfterTheCampaignClosed_ShouldStillReturnTheFirstPledge()
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsContribute] };
        var earlier = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1");
        var repository = new FakeContributionRepository(earlier);

        // The campaign is no longer active, which would reject a brand new pledge.
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: false),
            user, new FakeContributionDateTimeProvider(Now), repository, new FakeContributionTransactionExecutor(), new CrowdFunding.Modules.Contributions.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var replay = await handler.Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Equal(earlier.Id, replay.ContributionId);
    }

    [Theory]
    [InlineData(250, "USD")]
    [InlineData(100, "EUR")]
    public async Task Handle_SameKeyDifferentRequest_ShouldBeRejectedAsAConflict(int amount, string currency)
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsContribute] };
        var repository = new FakeContributionRepository(Contribution.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1"));
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakeContributionDateTimeProvider(Now), repository, new FakeContributionTransactionExecutor(), new CrowdFunding.Modules.Contributions.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            handler.Handle(new MakeContributionCommand(campaignId, amount, currency, IdempotencyKey: "k1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameKeyFromAnotherBacker_ShouldCreateAnotherPledge()
    {
        var campaignId = Guid.NewGuid();
        var otherBackersPledge = Contribution.Create(campaignId, Guid.NewGuid(), 100m, "USD", Now, idempotencyKey: "k1");
        var (handler, repository, _, _) = Build(new FakeContributionRepository(otherBackersPledge));

        var result = await handler.Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.NotEqual(otherBackersPledge.Id, result.ContributionId);
        Assert.NotNull(repository.SavedContribution);
    }

    [Fact]
    public async Task Handle_WithoutAKey_IsNotDeduplicatedByKey_TheOneLivePledgeRuleStopsTheSecond()
    {
        var (handler, repository, executor, _) = Build();
        var command = new MakeContributionCommand(Guid.NewGuid(), 100m, "USD");

        await handler.Handle(command, CancellationToken.None);
        // The first pledge is now live for this backer on this campaign, so a second request with no key
        // is refused by the one-live-pledge rule, not treated as a retry.
        await Assert.ThrowsAsync<ResourceConflictException>(() => handler.Handle(command, CancellationToken.None));

        Assert.NotNull(repository.SavedContribution);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task Handle_LosingTheInsertRace_ShouldAnswerWithTheWinningPledge()
    {
        var campaignId = Guid.NewGuid();
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.CampaignsContribute] };
        var winner = Contribution.Create(campaignId, user.UserId, 100m, "USD", Now, idempotencyKey: "k1");
        var repository = new RaceLosingRepository(winner);
        var handler = new MakeContributionCommandHandler(
            new FakeActiveCampaignCacheRepository(exists: true, isActive: true),
            user, new FakeContributionDateTimeProvider(Now), repository, new RaceLosingExecutor(), new CrowdFunding.Modules.Contributions.Infrastructure.Payments.FakePaymentProvider(), new FakePaymentSettings());

        var result = await handler.Handle(new MakeContributionCommand(campaignId, 100m, "USD", IdempotencyKey: "k1"), CancellationToken.None);

        Assert.True(result.IsReplay);
        Assert.Equal(winner.Id, result.ContributionId);
    }

    /// <summary>Reports no pledge on the first lookup (the race has not happened yet) and the winner on the second.</summary>
    private sealed class RaceLosingRepository(Contribution winner) : FakeContributionRepositoryBase
    {
        private int _lookups;

        public override Task<Contribution?> GetByIdempotencyKeyAsync(Guid contributorId, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult<Contribution?>(++_lookups == 1 ? null : winner);
    }

    /// <summary>Behaves like the real executor when the unique index rejects the insert.</summary>
    private sealed class RaceLosingExecutor : IContributionTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
            => throw new IdempotencyKeyAlreadyUsedException(new InvalidOperationException("unique violation"));

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
            => throw new IdempotencyKeyAlreadyUsedException(new InvalidOperationException("unique violation"));
    }

    private abstract class FakeContributionRepositoryBase : IContributionRepository
    {
        public Task AddAsync(Contribution contribution, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken) => Task.FromResult<Contribution?>(null);
        public Task<Contribution?> GetByExternalPaymentIntentIdAsync(string externalPaymentIntentId, CancellationToken cancellationToken) => Task.FromResult<Contribution?>(null);
        public abstract Task<Contribution?> GetByIdempotencyKeyAsync(Guid contributorId, string idempotencyKey, CancellationToken cancellationToken);
        public Task<Contribution?> GetLivePledgeAsync(Guid campaignId, Guid contributorId, CancellationToken cancellationToken) => Task.FromResult<Contribution?>(null);
        public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Contribution>> GetSucceededByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Contribution>>([]);
        public Task<IReadOnlyList<Contribution>> GetSucceededBatchByCampaignIdAsync(Guid campaignId, int batchSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Contribution>>([]);
    }
}
