using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Domain.Events;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.UnitTests;

public sealed class ContributionDomainTests
{
    [Fact]
    public void Create_ShouldUseSharedMoneyValueObjectAndStartPending()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25.555m,
            "usd",
            createdAtUtc);

        Assert.Equal(new Money(25.56m, "USD"), contribution.Money);
        Assert.Equal(ContributionStatus.Pending, contribution.Status);
        Assert.Equal(createdAtUtc, contribution.CreatedAtUtc);
        Assert.Empty(contribution.DomainEvents);
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsNotPositive()
    {
        var action = () => Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "USD",
            DateTime.UtcNow);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Contribution amount must be greater than zero. (Parameter 'amount')", exception.Message);
    }

    [Fact]
    public void ConfirmPayment_ShouldMarkContributionSucceeded_AndRaiseDomainEvent()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        contribution.ConfirmPayment("PAY-123", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Succeeded, contribution.Status);
        Assert.Equal("PAY-123", contribution.PaymentReference);
        Assert.Null(contribution.FailureReason);
        Assert.NotNull(contribution.ProcessedAtUtc);
        Assert.Contains(contribution.DomainEvents, domainEvent => domainEvent is ContributionPaymentConfirmedDomainEvent);
    }

    [Fact]
    public void FailPayment_ShouldMarkContributionFailed()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        contribution.FailPayment("Card declined.", new DateTime(2026, 4, 6, 12, 15, 0, DateTimeKind.Utc));

        Assert.Equal(ContributionStatus.Failed, contribution.Status);
        Assert.Equal("Card declined.", contribution.FailureReason);
        Assert.Null(contribution.PaymentReference);
        Assert.NotNull(contribution.ProcessedAtUtc);
    }
}

public sealed class MakeContributionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePendingContributionWithoutApplyingCampaignFunding()
    {
        var campaignReader = new FakeCampaignContributionAvailabilityReader(exists: true, canAcceptContributions: true, status: "Published");
        var repository = new FakeContributionRepository();
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Permissions = [PermissionConstants.CampaignsContribute]
        };
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new MakeContributionCommandHandler(
            campaignReader,
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var command = new MakeContributionCommand(
            Guid.NewGuid(),
            100m,
            "usd");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ContributionId);
        Assert.Equal("Pending", result.Status);
        Assert.NotNull(repository.SavedContribution);
        Assert.Equal(currentUser.UserId, repository.SavedContribution!.ContributorId);
        Assert.Equal(ContributionStatus.Pending, repository.SavedContribution.Status);
        Assert.Equal(new Money(100m, "USD"), repository.SavedContribution.Money);
        Assert.Equal(result.ContributionId, repository.SavedContribution.Id);
        Assert.Equal(command.CampaignId, campaignReader.CheckedCampaignId);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }
}

public sealed class ConfirmContributionPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistSucceededContributionAndLeaveCampaignReactionToEvents()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.ContributionsPaymentsManage]
        };
        var campaignReader = new FakeCampaignContributionAvailabilityReader(exists: true, canAcceptContributions: true, status: "Published");
        var repository = new FakeContributionRepository(contribution);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new ConfirmContributionPaymentCommandHandler(
            campaignReader,
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new ConfirmContributionPaymentCommand(contribution.CampaignId, contribution.Id, "PAY-123"),
            CancellationToken.None);

        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("PAY-123", result.PaymentReference);
        Assert.True(repository.WasUpdated);
        Assert.Equal(ContributionStatus.Succeeded, contribution.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
        Assert.Contains(contribution.DomainEvents, domainEvent => domainEvent is ContributionPaymentConfirmedDomainEvent);
    }
}

public sealed class FailContributionPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistFailedContribution()
    {
        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var currentUser = new TestCurrentUser
        {
            Permissions = [PermissionConstants.ContributionsPaymentsManage]
        };
        var repository = new FakeContributionRepository(contribution);
        var transactionExecutor = new FakeContributionTransactionExecutor();
        var handler = new FailContributionPaymentCommandHandler(
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 5, 0, DateTimeKind.Utc)),
            repository,
            transactionExecutor);

        var result = await handler.Handle(
            new FailContributionPaymentCommand(contribution.CampaignId, contribution.Id, "Card declined."),
            CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("Card declined.", result.FailureReason);
        Assert.True(repository.WasUpdated);
        Assert.Equal(ContributionStatus.Failed, contribution.Status);
        Assert.Equal(1, transactionExecutor.InvocationCount);
    }
}

public sealed class ListContributionsByCampaignQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnContributionReadModels()
    {
        IReadOnlyCollection<ListContributionsByCampaignResult> contributions =
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                75m,
                "USD",
                "Succeeded",
                "PAY-123",
                null,
                new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 6, 12, 10, 0, DateTimeKind.Utc))
        ];

        var page = new PagedResult<ListContributionsByCampaignResult>(contributions, 1, 10, 1);
        var readService = new FakeContributionReadService(page);
        var handler = new ListContributionsByCampaignQueryHandler(readService);
        var filter = new ListContributionsByCampaignFilter(Guid.NewGuid(), "USD", "Succeeded");

        var result = await handler.Handle(
            new ListContributionsByCampaignQuery(
                contributions.Single().CampaignId,
                new PageRequest(1, 10),
                filter),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(75m, result.Items.Single().Amount);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(new PageRequest(1, 10), readService.ReceivedPageRequest);
        Assert.Equal(filter, readService.ReceivedFilter);
    }
}

internal sealed class FakeContributionRepository : IContributionRepository
{
    private readonly Contribution? _contribution;

    public FakeContributionRepository(Contribution? contribution = null)
    {
        _contribution = contribution;
    }

    public Contribution? SavedContribution { get; private set; }
    public bool WasUpdated { get; private set; }

    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        SavedContribution = contribution;
        return Task.CompletedTask;
    }

    public Task<Contribution?> GetByIdAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        if (_contribution?.Id == contributionId)
        {
            return Task.FromResult<Contribution?>(_contribution);
        }

        return Task.FromResult(SavedContribution?.Id == contributionId ? SavedContribution : null);
    }

    public Task UpdateAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedContribution = contribution;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCampaignContributionAvailabilityReader : ICampaignContributionAvailabilityReader
{
    private readonly bool _exists;
    private readonly bool _canAcceptContributions;
    private readonly string _status;

    public FakeCampaignContributionAvailabilityReader(bool exists, bool canAcceptContributions, string status)
    {
        _exists = exists;
        _canAcceptContributions = canAcceptContributions;
        _status = status;
    }

    public Guid? CheckedCampaignId { get; private set; }

    public Task<GetCampaignContributionAvailabilityResult> GetCampaignContributionAvailabilityAsync(
        GetCampaignContributionAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        CheckedCampaignId = query.CampaignId;
        return Task.FromResult(new GetCampaignContributionAvailabilityResult(
            query.CampaignId,
            _exists,
            _canAcceptContributions,
            _status));
    }
}

internal sealed class FakeContributionDateTimeProvider : IContributionDateTimeProvider
{
    public FakeContributionDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

internal sealed class FakeContributionReadService : IContributionReadService
{
    private readonly PagedResult<ListContributionsByCampaignResult> _contributionsPage;

    public FakeContributionReadService(PagedResult<ListContributionsByCampaignResult> contributionsPage)
    {
        _contributionsPage = contributionsPage;
    }

    public PageRequest? ReceivedPageRequest { get; private set; }

    public ListContributionsByCampaignFilter? ReceivedFilter { get; private set; }

    public Task<PagedResult<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListContributionsByCampaignFilter filter,
        CancellationToken cancellationToken)
    {
        ReceivedPageRequest = pageRequest;
        ReceivedFilter = filter;
        return Task.FromResult(_contributionsPage);
    }
}
