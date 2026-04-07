using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;

namespace CrowdFunding.UnitTests;

public sealed class CampaignDomainTests
{
    [Fact]
    public void Create_ShouldInitializeDraftCampaignWithZeroRaisedAmount()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Launch a clean architecture sample",
            "We are building a modular clean architecture sample project.",
            "Technology",
            new Money(5000m, "usd"),
            createdAtUtc.AddDays(30),
            createdAtUtc);

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(new Money(0m, "USD"), campaign.RaisedAmount);
        Assert.Equal(new Money(5000m, "USD"), campaign.GoalAmount);
    }

    [Fact]
    public void Publish_ShouldMoveDraftCampaignToPublished()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        campaign.Publish(createdAtUtc.AddDays(1));

        Assert.Equal(CampaignStatus.Published, campaign.Status);
    }

    [Fact]
    public void Publish_ShouldThrow_WhenDeadlineHasAlreadyPassed()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var deadlineUtc = createdAtUtc.AddDays(2);
        var campaign = CreateDraftCampaign(createdAtUtc, deadlineUtc);

        var action = () => campaign.Publish(deadlineUtc);

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Cannot publish a campaign with a past deadline.", exception.Message);
    }

    [Fact]
    public void Cancel_ShouldMoveCampaignToCancelled()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = CreateDraftCampaign(createdAtUtc, createdAtUtc.AddDays(14));

        campaign.Cancel();

        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
    }

    private static Campaign CreateDraftCampaign(DateTime createdAtUtc, DateTime deadlineUtc)
    {
        return Campaign.Create(
            Guid.NewGuid(),
            "Save the community library",
            "This campaign helps renovate and reopen the local community library.",
            "Community",
            new Money(10000m, "USD"),
            deadlineUtc,
            createdAtUtc);
    }
}

public sealed class PublishCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPublishCampaignAndPersistChanges()
    {
        var now = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Fund a neighborhood garden",
            "This campaign builds a shared neighborhood garden for local families.",
            "Community",
            new Money(2500m, "USD"),
            now.AddDays(10),
            now.AddDays(-1));

        var repository = new FakeCampaignRepository(campaign);
        var handler = new PublishCampaignCommandHandler(repository, new FakeDateTimeProvider(now));

        var result = await handler.Handle(new PublishCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal("Published", result.Status);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.True(repository.WasUpdated);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCampaignDoesNotExist()
    {
        var handler = new PublishCampaignCommandHandler(
            new FakeCampaignRepository(),
            new FakeDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)));

        var action = async () => await handler.Handle(new PublishCampaignCommand(Guid.NewGuid()), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }
}

public sealed class CancelCampaignCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCancelCampaignAndPersistChanges()
    {
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Restore the old theater",
            "This campaign restores the old theater as a shared cultural venue.",
            "Culture",
            new Money(12000m, "USD"),
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        var repository = new FakeCampaignRepository(campaign);
        var handler = new CancelCampaignCommandHandler(repository);

        var result = await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
        Assert.True(repository.WasUpdated);
    }
}

public sealed class ListCampaignsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnCampaignsFromReadService()
    {
        IReadOnlyCollection<ListCampaignsResult> campaigns =
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Community Kitchen",
                "Community",
                8000m,
                "USD",
                1200m,
                "USD",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                "Published",
                new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc))
        ];

        var page = new PagedResult<ListCampaignsResult>(campaigns, 2, 5, 8);
        var readService = new FakeCampaignReadService(page);
        var handler = new ListCampaignsQueryHandler(readService);
        var filter = new ListCampaignsFilter(Guid.NewGuid(), "Community", "Published");

        var result = await handler.Handle(
            new ListCampaignsQuery(new PageRequest(2, 5), filter),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Community Kitchen", result.Items.Single().Title);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(8, result.TotalCount);
        Assert.Equal(new PageRequest(2, 5), readService.ReceivedPageRequest);
        Assert.Equal(filter, readService.ReceivedFilter);
    }
}

internal sealed class FakeCampaignRepository : ICampaignRepository
{
    private readonly Campaign? _campaign;

    public FakeCampaignRepository(Campaign? campaign = null)
    {
        _campaign = campaign;
    }

    public bool WasUpdated { get; private set; }

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_campaign?.Id == campaignId ? _campaign : null);
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCampaignReadService : ICampaignReadService
{
    private readonly PagedResult<ListCampaignsResult> _campaignsPage;

    public FakeCampaignReadService(PagedResult<ListCampaignsResult> campaignsPage)
    {
        _campaignsPage = campaignsPage;
    }

    public PageRequest? ReceivedPageRequest { get; private set; }

    public ListCampaignsFilter? ReceivedFilter { get; private set; }

    public Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<PagedResult<ListCampaignsResult>> ListAsync(
        PageRequest pageRequest,
        ListCampaignsFilter filter,
        CancellationToken cancellationToken)
    {
        ReceivedPageRequest = pageRequest;
        ReceivedFilter = filter;
        return Task.FromResult(_campaignsPage);
    }
}

internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}
