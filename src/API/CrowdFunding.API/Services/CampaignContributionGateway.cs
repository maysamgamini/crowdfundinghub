using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.API.Services;

public sealed class CampaignContributionGateway : ICampaignContributionGateway
{
    private readonly ICampaignFundingService _campaignFundingService;

    public CampaignContributionGateway(ICampaignFundingService campaignFundingService)
    {
        _campaignFundingService = campaignFundingService;
    }

    public Task ApplyContributionAsync(
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        return _campaignFundingService.AddContributionAsync(
            campaignId,
            amount,
            currency,
            cancellationToken);
    }
}
