using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Contracts;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.UnitTests;

public sealed class ContributionDomainTests
{
    [Fact]
    public void Create_ShouldUseSharedMoneyValueObject()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var contribution = Contribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25.555m,
            "usd",
            createdAtUtc);

        Assert.Equal(new Money(25.56m, "USD"), contribution.Money);
        Assert.Equal(createdAtUtc, contribution.CreatedAtUtc);
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
}

public sealed class MakeContributionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldApplyCampaignContributionAndPersistContribution()
    {
        var campaignsModule = new FakeCampaignsModule();
        var repository = new FakeContributionRepository();
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid() };
        var handler = new MakeContributionCommandHandler(
            campaignsModule,
            currentUser,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository);

        var command = new MakeContributionCommand(
            Guid.NewGuid(),
            100m,
            "usd");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ContributionId);
        Assert.Equal(command.CampaignId, campaignsModule.CampaignId);
        Assert.Equal(100m, campaignsModule.Amount);
        Assert.Equal("USD", campaignsModule.Currency);
        Assert.NotNull(repository.SavedContribution);
        Assert.Equal(currentUser.UserId, repository.SavedContribution!.ContributorId);
        Assert.Equal(new Money(100m, "USD"), repository.SavedContribution.Money);
        Assert.Equal(result.ContributionId, repository.SavedContribution.Id);
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
                new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc))
        ];

        var page = new PagedResult<ListContributionsByCampaignResult>(contributions, 1, 10, 1);
        var readService = new FakeContributionReadService(page);
        var handler = new ListContributionsByCampaignQueryHandler(readService);
        var filter = new ListContributionsByCampaignFilter(Guid.NewGuid(), "USD");

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
    public Contribution? SavedContribution { get; private set; }

    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        SavedContribution = contribution;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCampaignsModule : ICampaignsModule
{
    public Guid CampaignId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    public Task<AddContributionToCampaignResult> AddContributionToCampaignAsync(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        CampaignId = command.CampaignId;
        Amount = command.Amount;
        Currency = command.Currency;
        return Task.FromResult(new AddContributionToCampaignResult(command.CampaignId, command.Amount, command.Currency));
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
