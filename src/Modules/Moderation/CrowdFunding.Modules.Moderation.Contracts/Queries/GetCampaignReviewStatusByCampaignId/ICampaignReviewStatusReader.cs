namespace CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

/// <summary>
/// Defines cross-module reads for campaign review status.
/// </summary>
public interface ICampaignReviewStatusReader
{
    Task<GetCampaignReviewStatusByCampaignIdResult> GetCampaignReviewStatusByCampaignIdAsync(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken);
}
