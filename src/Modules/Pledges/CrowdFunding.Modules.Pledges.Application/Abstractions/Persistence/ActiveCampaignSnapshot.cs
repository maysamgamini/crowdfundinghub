namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

/// <summary>
/// A read-only snapshot of a campaign as replicated into the Pledges module's own local
/// schema — see <see cref="IActiveCampaignCacheRepository"/>.
/// </summary>
public sealed record ActiveCampaignSnapshot(Guid CampaignId, Guid OwnerId, string Currency, bool IsActive, DateTime DeadlineUtc);
