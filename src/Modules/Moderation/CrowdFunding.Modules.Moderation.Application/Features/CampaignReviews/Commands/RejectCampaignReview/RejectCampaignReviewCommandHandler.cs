using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

public sealed class RejectCampaignReviewCommandHandler : ICommandHandler<RejectCampaignReviewCommand, RejectCampaignReviewResult>
{
    private readonly ICampaignReviewRepository _campaignReviewRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IModerationDateTimeProvider _dateTimeProvider;
    private readonly IModerationTransactionExecutor _transactionExecutor;

    public RejectCampaignReviewCommandHandler(
        ICampaignReviewRepository campaignReviewRepository,
        ICurrentUser currentUser,
        IModerationDateTimeProvider dateTimeProvider,
        IModerationTransactionExecutor transactionExecutor)
    {
        _campaignReviewRepository = campaignReviewRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<RejectCampaignReviewResult> Handle(RejectCampaignReviewCommand command, CancellationToken cancellationToken)
    {
        EnsureCanReview();

        var campaignReview = await _campaignReviewRepository.GetByCampaignIdAsync(command.CampaignId, cancellationToken);
        if (campaignReview is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{command.CampaignId}' was not found.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            campaignReview.Reject(_currentUser.UserId, command.Notes, _dateTimeProvider.UtcNow);
            await _campaignReviewRepository.UpdateAsync(campaignReview, ct);
            return 0;
        }, cancellationToken);

        return new RejectCampaignReviewResult(command.CampaignId, campaignReview.Status.ToString());
    }

    private void EnsureCanReview()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to reject a campaign review.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.ModerationReview))
        {
            throw new ForbiddenAccessException("The current user does not have permission to review campaigns.");
        }
    }
}