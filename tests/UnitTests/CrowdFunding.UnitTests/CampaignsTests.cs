using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Domain.ValueObjects;

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

    private sealed class FakeCampaignRepository : ICampaignRepository
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

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public FakeDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
