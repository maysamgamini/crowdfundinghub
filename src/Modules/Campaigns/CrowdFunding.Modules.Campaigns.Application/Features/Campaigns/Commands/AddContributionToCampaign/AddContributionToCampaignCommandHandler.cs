using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;

public sealed class AddContributionToCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;

    public AddContributionToCampaignCommandHandler(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<AddContributionToCampaignResult> Handle(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        campaign.AddContribution(new Money(command.Amount, command.Currency));

        await _campaignRepository.UpdateAsync(campaign, cancellationToken);

        return new AddContributionToCampaignResult(
            campaign.Id,
            campaign.RaisedAmount.Amount,
            campaign.RaisedAmount.Currency);
    }
}
