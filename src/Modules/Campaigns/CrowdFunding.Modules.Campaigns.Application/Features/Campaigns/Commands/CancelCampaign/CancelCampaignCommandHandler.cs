using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

public sealed class CancelCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUser _currentUser;

    public CancelCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICurrentUser currentUser)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
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

        EnsureCanManageCampaign(campaign.OwnerId);
        campaign.Cancel();

        await _campaignRepository.UpdateAsync(campaign, cancellationToken);

        return new CancelCampaignResult(campaign.Id, campaign.Status.ToString());
    }

    private void EnsureCanManageCampaign(Guid ownerId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to cancel a campaign.");
        }

        if (_currentUser.UserId == ownerId || _currentUser.HasPermission("campaigns.manage.any"))
        {
            return;
        }

        throw new ForbiddenAccessException("Only the campaign owner or an administrator can cancel this campaign.");
    }
}
