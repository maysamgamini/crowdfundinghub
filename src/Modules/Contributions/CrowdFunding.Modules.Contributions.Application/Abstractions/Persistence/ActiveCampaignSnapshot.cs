namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>
/// A read-only snapshot of a campaign as replicated into the Contributions module's own local
/// schema — see <see cref="IActiveCampaignCacheRepository"/>.
/// </summary>
public sealed record ActiveCampaignSnapshot(Guid CampaignId, string Currency, bool IsActive, DateTime DeadlineUtc);
