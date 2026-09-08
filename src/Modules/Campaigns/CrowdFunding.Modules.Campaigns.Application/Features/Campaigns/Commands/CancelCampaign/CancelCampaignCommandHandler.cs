using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

/// <summary>
/// Handles Cancel Campaign command requests.
/// </summary>
public sealed class CancelCampaignCommandHandler : ICommandHandler<CancelCampaignCommand, CancelCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public CancelCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICurrentUser currentUser,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<CancelCampaignResult> Handle(CancelCampaignCommand command, CancellationToken cancellationToken)
    {
        // Same advisory lock as AddContributionToCampaignCommandHandler, and the campaign is
        // (re-)loaded inside it. Previously this loaded the campaign and ran Cancel() outside
        // any lock, so a confirmed pledge being applied concurrently could interleave with a
        // cancellation instead of being serialized against it.
        var advisoryLockKey = AdvisoryLockKey.FromGuid(command.CampaignId);
        var campaignId = Guid.Empty;
        var status = string.Empty;

        await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
        {
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            EnsureCanManageCampaign(campaign.OwnerId);

            campaign.Cancel();
            await _campaignRepository.UpdateAsync(campaign, ct);

            campaignId = campaign.Id;
            status = campaign.Status.ToString();

            return 0;
        }, cancellationToken);

        return new CancelCampaignResult(campaignId, status);
    }

    private void EnsureCanManageCampaign(Guid ownerId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to cancel a campaign.");
        }

        var canManageAny = _currentUser.HasPermission(PermissionConstants.CampaignsManageAny);
        if (_currentUser.UserId == ownerId && _currentUser.HasPermission(PermissionConstants.CampaignsCancel))
        {
            return;
        }

        if (canManageAny)
        {
            return;
        }

        throw new ForbiddenAccessException("Only a permitted campaign owner or an administrator can cancel this campaign.");
    }
}
