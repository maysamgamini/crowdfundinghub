using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewStatusByCampaignId;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

public sealed class CampaignReviewStatusReader : ICampaignReviewStatusReader
{
    private readonly GetCampaignReviewStatusByCampaignIdQueryHandler _handler;

    public CampaignReviewStatusReader(GetCampaignReviewStatusByCampaignIdQueryHandler handler)
    {
        _handler = handler;
    }

    public Task<GetCampaignReviewStatusByCampaignIdResult> GetCampaignReviewStatusByCampaignIdAsync(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        return _handler.Handle(query, cancellationToken);
    }
}
