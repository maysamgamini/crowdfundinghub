using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

public sealed class PublishCampaignCommandHandler : ICommandHandler<PublishCampaignCommand, PublishCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignReviewStatusReader _campaignReviewStatusReader;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public PublishCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ICampaignReviewStatusReader campaignReviewStatusReader,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _campaignReviewStatusReader = campaignReviewStatusReader;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<PublishCampaignResult> Handle(PublishCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);
        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        EnsureCanManageCampaign(campaign.OwnerId);

        var review = await _campaignReviewStatusReader.GetCampaignReviewStatusByCampaignIdAsync(
            new GetCampaignReviewStatusByCampaignIdQuery(command.CampaignId),
            cancellationToken);

        if (!string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Campaign must be approved by moderation before it can be published.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            campaign.Publish(_dateTimeProvider.UtcNow);
            await _campaignRepository.UpdateAsync(campaign, ct);
            return 0;
        }, cancellationToken);

        return new PublishCampaignResult(campaign.Id, campaign.Status.ToString());
    }

    private void EnsureCanManageCampaign(Guid ownerId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to publish a campaign.");
        }

        var canManageAny = _currentUser.HasPermission(PermissionConstants.CampaignsManageAny);
        if (_currentUser.UserId == ownerId && _currentUser.HasPermission(PermissionConstants.CampaignsPublish))
        {
            return;
        }

        if (canManageAny)
        {
            return;
        }

        throw new ForbiddenAccessException("Only a permitted campaign owner or an administrator can publish this campaign.");
    }
}