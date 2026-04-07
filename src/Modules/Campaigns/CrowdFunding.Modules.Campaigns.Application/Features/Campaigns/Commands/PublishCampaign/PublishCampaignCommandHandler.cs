using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Contracts;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

public sealed class PublishCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IModerationModule _moderationModule;

    public PublishCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IDateTimeProvider dateTimeProvider,
        IModerationModule moderationModule)
    {
        _campaignRepository = campaignRepository;
        _dateTimeProvider = dateTimeProvider;
        _moderationModule = moderationModule;
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

        var review = await _moderationModule.GetCampaignReviewStatusByCampaignIdAsync(
            new GetCampaignReviewStatusByCampaignIdQuery(command.CampaignId),
            cancellationToken);

        if (!string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Campaign must be approved by moderation before it can be published.");
        }

        campaign.Publish(_dateTimeProvider.UtcNow);

        await _campaignRepository.UpdateAsync(campaign, cancellationToken);

        return new PublishCampaignResult(campaign.Id, campaign.Status.ToString());
    }
}
