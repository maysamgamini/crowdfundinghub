using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using Microsoft.AspNetCore.SignalR;

namespace CrowdFunding.API.RealTime;

/// <summary>
/// Broadcasts pledge updates to <see cref="CampaignHub"/> group members. Exceptions are logged
/// and swallowed rather than propagated — a SignalR/transport hiccup must never fail the command
/// that just successfully credited the campaign (see ICampaignRealtimeNotifier's remarks: this is
/// a best-effort broadcast, not a durable message).
/// </summary>
public sealed class SignalRCampaignRealtimeNotifier : ICampaignRealtimeNotifier
{
    private readonly IHubContext<CampaignHub> _hubContext;
    private readonly ILogger<SignalRCampaignRealtimeNotifier> _logger;

    public SignalRCampaignRealtimeNotifier(IHubContext<CampaignHub> hubContext, ILogger<SignalRCampaignRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyPledgeReceivedAsync(
        Guid campaignId,
        decimal raisedAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(CampaignHub.GroupName(campaignId))
                .SendAsync("PledgeReceived", new { campaignId, raisedAmount, currency }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to broadcast pledge update for campaign {CampaignId}.", campaignId);
        }
    }
}
