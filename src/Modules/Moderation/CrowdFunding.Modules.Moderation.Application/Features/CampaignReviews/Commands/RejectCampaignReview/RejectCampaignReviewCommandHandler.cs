using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

public sealed class RejectCampaignReviewCommandHandler
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly IModerationDateTimeProvider _dateTimeProvider;

    public RejectCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        IModerationDateTimeProvider dateTimeProvider)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RejectCampaignReviewResult> Handle(
        RejectCampaignReviewCommand command,
        CancellationToken cancellationToken)
    {
        var campaignReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);

        if (campaignReview is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{command.CampaignId}' was not found.");
        }

        campaignReview.Reject(command.ModeratorId, command.Notes, _dateTimeProvider.UtcNow);

        await _campaignReviewRepository.UpdateAsync(campaignReview, cancellationToken);

        return new RejectCampaignReviewResult(command.CampaignId, campaignReview.Status.ToString());
    }
}
