namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Contributions' own local replica of the subset of Campaign state it needs to validate a
/// pledge (existence, currency, active/deadline status) — populated by
/// <c>ReplicatedCampaignEventHandlers</c> reacting to Campaigns' application events, never by a
/// synchronous cross-module read. See TICKET-023 / <c>IActiveCampaignCacheRepository</c>.
/// </summary>
public sealed class ActiveCampaignCache
{
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
