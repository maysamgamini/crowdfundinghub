using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries;

/// <summary>Who is asking to read pledges, and therefore how much of each pledge they may see.</summary>
internal enum PledgeViewer
{
    /// <summary>Anyone signed in who is neither the campaign's owner nor a payments manager: only their own pledges.</summary>
    Backer,

    /// <summary>The campaign's owner: every pledge on the campaign, without payment references.</summary>
    CampaignOwner,

    /// <summary>A holder of the payments-manage permission: everything.</summary>
    PaymentsManager,
}

internal static class PledgeVisibility
{
    /// <summary>Works out the viewer's role for one campaign. The owner comes from Pledges' own replicated copy of the campaign, so no call into Campaigns is needed.</summary>
    public static async Task<PledgeViewer> ResolveAsync(
        ICurrentUser currentUser,
        IActiveCampaignCacheRepository campaigns,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to read pledges.");
        }

        if (currentUser.HasPermission(PermissionConstants.PledgesPaymentsManage))
        {
            return PledgeViewer.PaymentsManager;
        }

        var campaign = await campaigns.GetAsync(campaignId, cancellationToken);
        return campaign is not null && campaign.OwnerId == currentUser.UserId
            ? PledgeViewer.CampaignOwner
            : PledgeViewer.Backer;
    }
}
