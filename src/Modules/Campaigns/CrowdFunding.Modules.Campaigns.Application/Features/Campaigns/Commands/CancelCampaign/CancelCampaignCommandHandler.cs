using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

public sealed class CancelCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;

    public CancelCampaignCommandHandler(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<CancelCampaignResult> Handle(
        CancelCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        campaign.Cancel();

        await _campaignRepository.UpdateAsync(campaign, cancellationToken);

        return new CancelCampaignResult(campaign.Id, campaign.Status.ToString());
    }
}
