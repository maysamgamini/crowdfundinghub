using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface ICampaignReadService
{
    Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ListCampaignsResult>> ListAsync(CancellationToken cancellationToken);
}
