namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

/// <summary>
/// Pushes live campaign-funding updates to connected clients (improvement.md §5.1) so backers
/// watching a campaign page see the progress bar and raised total update instantly instead of
/// needing to poll. This is a best-effort broadcast, not a durable message: unlike the outbox,
/// a dropped notification here is not retried — a client that missed one still sees the correct
/// total on its next page load/query.
/// </summary>
public interface ICampaignRealtimeNotifier
{
    Task NotifyPledgeReceivedAsync(
        Guid campaignId,
        decimal raisedAmount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Broadcasts that a reward tier has just claimed/reserved its last available slot,
    /// so connected clients can disable the "Select Tier" button in real time (TICKET-034).
    /// Same best-effort, non-durable contract as <see cref="NotifyPledgeReceivedAsync"/>.</summary>
    Task NotifyRewardTierSoldOutAsync(
        Guid campaignId,
        Guid rewardTierId,
        string title,
        CancellationToken cancellationToken = default);
}
