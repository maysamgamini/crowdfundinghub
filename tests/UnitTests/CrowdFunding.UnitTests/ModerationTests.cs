using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using CrowdFunding.Modules.Moderation.Domain.Enums;

namespace CrowdFunding.UnitTests;

public sealed class CampaignReviewDomainTests
{
    [Fact]
    public void Create_ShouldInitializePendingReview()
    {
        var createdAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);

        var review = CampaignReview.Create(Guid.NewGuid(), createdAtUtc);

        Assert.Equal(CampaignReviewStatus.Pending, review.Status);
        Assert.Equal(createdAtUtc, review.CreatedAtUtc);
    }

    [Fact]
    public void Approve_ShouldMoveReviewToApproved()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));

        review.Approve(Guid.NewGuid(), "Approved after verification.", new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(CampaignReviewStatus.Approved, review.Status);
        Assert.NotNull(review.ModeratorId);
        Assert.NotNull(review.ReviewedAtUtc);
    }
}

public sealed class CreateCampaignReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePendingReview()
    {
        var repository = new FakeCampaignReviewRepository();
        var handler = new CreateCampaignReviewCommandHandler(
            repository,
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(new CreateCampaignReviewCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(repository.SavedReview);
        Assert.Equal(result.CampaignReviewId, repository.SavedReview!.Id);
        Assert.Equal("Pending", result.Status);
    }
}

public sealed class ApproveCampaignReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldApprovePendingReview()
    {
        var review = CampaignReview.Create(Guid.NewGuid(), new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeCampaignReviewRepository(review);
        var handler = new ApproveCampaignReviewCommandHandler(
            repository,
            new TestCurrentUser { UserId = Guid.NewGuid() },
            new FakeModerationDateTimeProvider(new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc)));

        var result = await handler.Handle(
            new ApproveCampaignReviewCommand(review.CampaignId, "Looks good."),
            CancellationToken.None);

        Assert.Equal("Approved", result.Status);
        Assert.Equal(CampaignReviewStatus.Approved, review.Status);
        Assert.True(repository.WasUpdated);
    }
}

public sealed class GetCampaignReviewByCampaignIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnReviewFromReadService()
    {
        var expected = new GetCampaignReviewByCampaignIdResult(
            Guid.NewGuid(),
            "Pending",
            null,
            null,
            new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc),
            null);

        var handler = new GetCampaignReviewByCampaignIdQueryHandler(new FakeCampaignReviewReadService(expected));

        var result = await handler.Handle(new GetCampaignReviewByCampaignIdQuery(expected.CampaignId), CancellationToken.None);

        Assert.Equal(expected, result);
    }
}

internal sealed class FakeCampaignReviewRepository : ICampaignReviewRepository
{
    private readonly CampaignReview? _review;

    public FakeCampaignReviewRepository(CampaignReview? review = null)
    {
        _review = review;
    }

    public CampaignReview? SavedReview { get; private set; }

    public bool WasUpdated { get; private set; }

    public Task AddAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        SavedReview = campaignReview;
        return Task.CompletedTask;
    }

    public Task<CampaignReview?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (_review?.CampaignId == campaignId)
        {
            return Task.FromResult<CampaignReview?>(_review);
        }

        return Task.FromResult(SavedReview?.CampaignId == campaignId ? SavedReview : null);
    }

    public Task UpdateAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        WasUpdated = true;
        SavedReview = campaignReview;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCampaignReviewReadService : ICampaignReviewReadService
{
    private readonly GetCampaignReviewByCampaignIdResult? _result;

    public FakeCampaignReviewReadService(GetCampaignReviewByCampaignIdResult? result)
    {
        _result = result;
    }

    public Task<GetCampaignReviewByCampaignIdResult?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_result);
    }
}

internal sealed class FakeModerationDateTimeProvider : IModerationDateTimeProvider
{
    public FakeModerationDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}
