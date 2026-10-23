using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

/// <summary>
/// Handles Get Campaign Review By Campaign Id query requests. Reading a review needs the moderation
/// permission, checked here so it holds in the monolith and in the extracted service alike.
/// </summary>
public sealed class GetCampaignReviewByCampaignIdQueryHandler : IQueryHandler<GetCampaignReviewByCampaignIdQuery, GetCampaignReviewByCampaignIdResult>
{
    private readonly ICampaignReviewReadService _campaignReviewReadService;
    private readonly ICurrentUser _currentUser;

    public GetCampaignReviewByCampaignIdQueryHandler(ICampaignReviewReadService campaignReviewReadService, ICurrentUser currentUser)
    {
        _campaignReviewReadService = campaignReviewReadService;
        _currentUser = currentUser;
    }

    public async Task<GetCampaignReviewByCampaignIdResult> Handle(
        GetCampaignReviewByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        ModerationReadAccess.EnsureCanRead(_currentUser);

        var result = await _campaignReviewReadService.GetByCampaignIdAsync(query.CampaignId, cancellationToken);

        return result ?? throw new KeyNotFoundException($"Campaign review for campaign '{query.CampaignId}' was not found.");
    }
}
