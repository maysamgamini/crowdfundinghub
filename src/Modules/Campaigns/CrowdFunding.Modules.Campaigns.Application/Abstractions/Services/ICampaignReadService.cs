using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface ICampaignReadService
{
    Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<PagedResult<ListCampaignsResult>> ListAsync(
        PageRequest pageRequest,
        ListCampaignsFilter filter,
        CancellationToken cancellationToken);
}
