using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

public sealed class PublishCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignModerationGateway _campaignModerationGateway;

    public PublishCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IDateTimeProvider dateTimeProvider,
        ICampaignModerationGateway campaignModerationGateway)
    {
        _campaignRepository = campaignRepository;
        _dateTimeProvider = dateTimeProvider;
        _campaignModerationGateway = campaignModerationGateway;
    }

    public async Task<PublishCampaignResult> Handle(
        PublishCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        await _campaignModerationGateway.EnsureApprovedForPublishingAsync(command.CampaignId, cancellationToken);

        campaign.Publish(_dateTimeProvider.UtcNow);

        await _campaignRepository.UpdateAsync(campaign, cancellationToken);

        return new PublishCampaignResult(campaign.Id, campaign.Status.ToString());
    }
}
