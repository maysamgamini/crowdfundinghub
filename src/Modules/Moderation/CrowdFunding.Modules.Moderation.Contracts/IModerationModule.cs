using CrowdFunding.Modules.Moderation.Contracts.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Moderation.Contracts;

public interface IModerationModule
{
    Task<CreateCampaignReviewResult> CreateCampaignReviewAsync(
        CreateCampaignReviewCommand command,
        CancellationToken cancellationToken);

    Task<GetCampaignReviewStatusByCampaignIdResult> GetCampaignReviewStatusByCampaignIdAsync(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken);
}
