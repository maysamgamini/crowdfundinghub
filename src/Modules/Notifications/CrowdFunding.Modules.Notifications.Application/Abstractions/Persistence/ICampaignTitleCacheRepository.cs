namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;

/// <summary>
/// Persists Notifications' own local replica of a campaign's title and owner (Event-Carried
/// State Transfer, populated by <c>ReplicatedCampaignTitleEventHandler</c> reacting to Campaigns'
/// <c>CampaignCreatedApplicationEvent</c>), so an email receipt/alert can name the campaign
/// without Notifications ever making a synchronous, in-process, cross-module call into Campaigns
/// — the same coupling TICKET-023 already eliminated for Contributions. <c>NetArchTest</c>
/// (<c>NotificationsModuleDependencyTests.Application_ShouldNotReference_OtherModulesInternals</c>)
/// enforces that Notifications' Application layer never references another module's Application,
/// Infrastructure, or Domain assembly — only Contracts events, which is exactly what this cache is
/// built from.
/// </summary>
public interface ICampaignTitleCacheRepository
{
    /// <summary>Inserts or refreshes the replicated row for a campaign.</summary>
    Task UpsertAsync(Guid campaignId, string title, Guid ownerId, DateTime updatedAtUtc, CancellationToken cancellationToken);

    /// <summary>Reads the current replicated snapshot for a campaign, or null if none has been
    /// replicated yet (the <c>CampaignCreatedApplicationEvent</c> hasn't been processed) — callers
    /// must tolerate this and fall back to a generic subject line rather than fail the email send.</summary>
    Task<CampaignTitleSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken);
}
