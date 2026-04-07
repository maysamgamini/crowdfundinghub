using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
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
        var campaignGateway = new FakeCampaignContributionGateway();
        var repository = new FakeContributionRepository();
        var handler = new MakeContributionCommandHandler(
            campaignGateway,
            new FakeContributionDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)),
            repository);

        var command = new MakeContributionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "usd");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ContributionId);
        Assert.Equal(command.CampaignId, campaignGateway.CampaignId);
        Assert.Equal(100m, campaignGateway.Amount);
        Assert.Equal("USD", campaignGateway.Currency);
        Assert.NotNull(repository.SavedContribution);
        Assert.Equal(new Money(100m, "USD"), repository.SavedContribution!.Money);
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

        var handler = new ListContributionsByCampaignQueryHandler(
            new FakeContributionReadService(contributions));

        var result = await handler.Handle(
            new ListContributionsByCampaignQuery(contributions.Single().CampaignId),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(75m, result.Single().Amount);
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

internal sealed class FakeCampaignContributionGateway : ICampaignContributionGateway
{
    public Guid CampaignId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    public Task ApplyContributionAsync(
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        CampaignId = campaignId;
        Amount = amount;
        Currency = currency;
        return Task.CompletedTask;
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
    private readonly IReadOnlyCollection<ListContributionsByCampaignResult> _contributions;

    public FakeContributionReadService(IReadOnlyCollection<ListContributionsByCampaignResult> contributions)
    {
        _contributions = contributions;
    }

    public Task<IReadOnlyCollection<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_contributions);
    }
}
