using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

namespace CrowdFunding.API.Services;

public sealed class CampaignModerationGateway : ICampaignModerationGateway
{
    private readonly CreateCampaignReviewCommandHandler _createCampaignReviewCommandHandler;
    private readonly GetCampaignReviewByCampaignIdQueryHandler _getCampaignReviewByCampaignIdQueryHandler;

    public CampaignModerationGateway(
        CreateCampaignReviewCommandHandler createCampaignReviewCommandHandler,
        GetCampaignReviewByCampaignIdQueryHandler getCampaignReviewByCampaignIdQueryHandler)
    {
        _createCampaignReviewCommandHandler = createCampaignReviewCommandHandler;
        _getCampaignReviewByCampaignIdQueryHandler = getCampaignReviewByCampaignIdQueryHandler;
    }

    public async Task CreateReviewAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        await _createCampaignReviewCommandHandler.Handle(
            new CreateCampaignReviewCommand(campaignId),
            cancellationToken);
    }

    public async Task EnsureApprovedForPublishingAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var review = await _getCampaignReviewByCampaignIdQueryHandler.Handle(
            new GetCampaignReviewByCampaignIdQuery(campaignId),
            cancellationToken);

        if (!string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Campaign must be approved by moderation before it can be published.");
        }
    }
}
