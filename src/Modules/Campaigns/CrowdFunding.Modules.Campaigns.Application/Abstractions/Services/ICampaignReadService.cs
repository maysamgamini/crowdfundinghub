using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface ICampaignReadService
{
    Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken);
}