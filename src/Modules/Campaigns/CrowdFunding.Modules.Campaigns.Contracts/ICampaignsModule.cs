using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

namespace CrowdFunding.Modules.Campaigns.Contracts;

public interface ICampaignsModule
{
    Task<AddContributionToCampaignResult> AddContributionToCampaignAsync(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken);
}
