using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

public sealed class CampaignsModule : ICampaignsModule
{
    private readonly AddContributionToCampaignCommandHandler _addContributionToCampaignCommandHandler;

    public CampaignsModule(AddContributionToCampaignCommandHandler addContributionToCampaignCommandHandler)
    {
        _addContributionToCampaignCommandHandler = addContributionToCampaignCommandHandler;
    }

    public Task<AddContributionToCampaignResult> AddContributionToCampaignAsync(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        return _addContributionToCampaignCommandHandler.Handle(command, cancellationToken);
    }
}
