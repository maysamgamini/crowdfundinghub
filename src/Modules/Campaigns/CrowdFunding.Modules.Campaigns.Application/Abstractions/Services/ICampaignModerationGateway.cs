namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface ICampaignModerationGateway
{
    Task CreateReviewAsync(Guid campaignId, CancellationToken cancellationToken);
    Task EnsureApprovedForPublishingAsync(Guid campaignId, CancellationToken cancellationToken);
}
