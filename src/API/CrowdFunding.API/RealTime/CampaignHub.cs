using Microsoft.AspNetCore.SignalR;

namespace CrowdFunding.API.RealTime;

/// <summary>
/// Real-time channel for campaign funding progress (improvement.md §5.1). Backers viewing a
/// campaign page join its group and receive a <c>PledgeReceived</c> push whenever a confirmed
/// contribution changes that campaign's raised total, instead of polling for updates.
/// </summary>
public sealed class CampaignHub : Hub
{
    public static string GroupName(Guid campaignId) => $"campaign_{campaignId}";

    public Task JoinCampaign(Guid campaignId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(campaignId));

    public Task LeaveCampaign(Guid campaignId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(campaignId));
}
