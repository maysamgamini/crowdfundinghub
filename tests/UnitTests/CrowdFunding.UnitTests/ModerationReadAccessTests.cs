using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.ListCampaignReviews;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Reviews are internal to moderation, so reading one is a moderator action. The rule lives in the handlers
/// so the extracted Moderation service, which has none of the monolith's authorization policies, enforces it too.
/// </summary>
public sealed class ModerationReadAccessTests
{
    private static readonly GetCampaignReviewByCampaignIdResult Review = new(
        Guid.NewGuid(), "Pending", null, null, new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc), null);

    private static GetCampaignReviewByCampaignIdQueryHandler GetHandler(TestCurrentUser user)
        => new(new FakeCampaignReviewReadService(Review), user);

    private static ListCampaignReviewsQueryHandler ListHandler(TestCurrentUser user)
        => new(new FakeCampaignReviewReadService(Review), user);

    private static TestCurrentUser Moderator() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionConstants.ModerationReview] };

    private static TestCurrentUser PlainUser() => new() { UserId = Guid.NewGuid() };

    private static TestCurrentUser Anonymous() => new() { UserId = Guid.Empty };

    [Fact]
    public async Task Get_AModerator_CanRead() =>
        Assert.Equal(Review, await GetHandler(Moderator()).Handle(new GetCampaignReviewByCampaignIdQuery(Review.CampaignId), CancellationToken.None));

    [Fact]
    public async Task Get_APlainSignedInUser_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            GetHandler(PlainUser()).Handle(new GetCampaignReviewByCampaignIdQuery(Review.CampaignId), CancellationToken.None));

    [Fact]
    public async Task Get_AnAnonymousCaller_IsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            GetHandler(Anonymous()).Handle(new GetCampaignReviewByCampaignIdQuery(Review.CampaignId), CancellationToken.None));

    [Fact]
    public async Task List_AModerator_CanRead() =>
        Assert.Single((await ListHandler(Moderator()).Handle(new ListCampaignReviewsQuery(new PageRequest(1, 20), null), CancellationToken.None)).Items);

    [Fact]
    public async Task List_APlainSignedInUser_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            ListHandler(PlainUser()).Handle(new ListCampaignReviewsQuery(new PageRequest(1, 20), null), CancellationToken.None));
}
