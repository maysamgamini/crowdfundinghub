using Microsoft.AspNetCore.SignalR;

namespace CrowdFunding.API.RealTime;

/// <summary>
/// Real-time channel for campaign funding progress (improvement.md §5.1). Backers viewing a
/// campaign page join its group and receive a <c>PledgeReceived</c> push whenever a confirmed
/// contribution changes that campaign's raised total, instead of polling for updates.
/// </summary>
public sealed class CampaignHub : Hub
{
    /// <summary>
    /// Computes the SignalR group name for the specified campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <returns>The formatted group name string.</returns>
    public static string GroupName(Guid campaignId) => $"campaign_{campaignId}";

    /// <summary>
    /// Adds the calling client connection to the real-time group for the specified campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <returns>A task representing the asynchronous group membership operation.</returns>
    public Task JoinCampaign(Guid campaignId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(campaignId));

    /// <summary>
    /// Removes the calling client connection from the real-time group for the specified campaign.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <returns>A task representing the asynchronous group membership operation.</returns>
    public Task LeaveCampaign(Guid campaignId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(campaignId));
}
