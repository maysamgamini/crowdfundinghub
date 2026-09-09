using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Moderation.Contracts.Enums;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

/// <summary>
/// Handles Publish Campaign command requests.
/// </summary>
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

    /// <inheritdoc/>
    public async Task<PublishCampaignResult> Handle(PublishCampaignCommand command, CancellationToken cancellationToken)
    {
        // Same advisory lock as AddContributionToCampaignCommandHandler/CancelCampaignCommandHandler,
        // and the campaign — plus its moderation status — is (re-)loaded inside it. Previously
        // this read the campaign and moderation status outside any lock, then published a stale
        // in-memory instance; a concurrent CancelCampaign could commit a cancellation in between,
        // and this handler would still overwrite it back to Published (TICKET-041).
        var advisoryLockKey = AdvisoryLockKey.FromGuid(command.CampaignId);
        var campaignId = Guid.Empty;
        var status = string.Empty;

        await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
        {
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            EnsureCanManageCampaign(campaign.OwnerId);

            var review = await _campaignReviewStatusReader.GetCampaignReviewStatusByCampaignIdAsync(
                new GetCampaignReviewStatusByCampaignIdQuery(command.CampaignId),
                ct);

            if (review.Status != CampaignReviewStatusContract.Approved)
            {
                // State-machine conflict (RFC 9110 §15.5.10), not a malformed request — 409 lets
                // clients distinguish this from a validation failure.
                throw new ResourceConflictException("Campaign must be approved by moderation before it can be published.");
            }

            campaign.Publish(_dateTimeProvider.UtcNow);
            await _campaignRepository.UpdateAsync(campaign, ct);

            campaignId = campaign.Id;
            status = campaign.Status.ToString();
        }, cancellationToken);

        return new PublishCampaignResult(campaignId, status);
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
