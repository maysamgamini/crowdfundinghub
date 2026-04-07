using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

public sealed class ApproveCampaignReviewCommandHandler
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IModerationDateTimeProvider _dateTimeProvider;

    public ApproveCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        ICurrentUser currentUser,
        IModerationDateTimeProvider dateTimeProvider)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ApproveCampaignReviewResult> Handle(
        ApproveCampaignReviewCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to approve a campaign review.");
        }

        var campaignReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);

        if (campaignReview is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{command.CampaignId}' was not found.");
        }

        campaignReview.Approve(_currentUser.UserId, command.Notes, _dateTimeProvider.UtcNow);

        await _campaignReviewRepository.UpdateAsync(campaignReview, cancellationToken);

        return new ApproveCampaignReviewResult(command.CampaignId, campaignReview.Status.ToString());
    }
}
