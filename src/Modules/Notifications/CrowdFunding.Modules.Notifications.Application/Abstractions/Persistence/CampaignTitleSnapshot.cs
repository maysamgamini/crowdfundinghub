namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;

/// <summary>
/// A read-only snapshot of a campaign's display metadata as replicated into the Notifications
/// module's own local schema — see <see cref="ICampaignTitleCacheRepository"/>.
/// </summary>
public sealed record CampaignTitleSnapshot(Guid CampaignId, string Title, Guid OwnerId);
