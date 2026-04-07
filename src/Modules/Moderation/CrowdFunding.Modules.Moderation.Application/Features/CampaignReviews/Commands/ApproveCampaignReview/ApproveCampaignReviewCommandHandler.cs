using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

public sealed class ApproveCampaignReviewCommandHandler
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly IModerationDateTimeProvider _dateTimeProvider;

    public ApproveCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        IModerationDateTimeProvider dateTimeProvider)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ApproveCampaignReviewResult> Handle(
        ApproveCampaignReviewCommand command,
        CancellationToken cancellationToken)
    {
        var campaignReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);

        if (campaignReview is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{command.CampaignId}' was not found.");
        }

        campaignReview.Approve(command.ModeratorId, command.Notes, _dateTimeProvider.UtcNow);

        await _campaignReviewRepository.UpdateAsync(campaignReview, cancellationToken);

        return new ApproveCampaignReviewResult(command.CampaignId, campaignReview.Status.ToString());
    }
}
