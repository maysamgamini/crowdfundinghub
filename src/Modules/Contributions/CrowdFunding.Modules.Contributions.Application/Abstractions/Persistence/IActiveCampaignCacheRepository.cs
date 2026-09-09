namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>
/// Persists the Contributions module's own local replica of campaign state (Event-Carried State
/// Transfer, populated by <c>ReplicatedCampaignEventHandlers</c> reacting to Campaigns'
/// application events), so pledge validation never makes a synchronous, in-process, cross-module
/// call into Campaigns' database — the coupling TICKET-023 exists to eliminate. When Contributions
/// is ever extracted into its own service, this repository's implementation is the only piece
/// that needs a schema (already owned locally); no application/command-handler code changes.
/// </summary>
public interface IActiveCampaignCacheRepository
{
    /// <summary>Inserts or refreshes the replicated row for a campaign.</summary>
    Task UpsertAsync(
        Guid campaignId,
        string title,
        string currency,
        bool isActive,
        DateTime deadlineUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Flips the active flag on an existing replicated row (Published -&gt; true,
    /// Cancelled -&gt; false). A no-op if the row hasn't been replicated yet — it will be created
    /// with the caller's desired mid-flight status the next time the row-owning
    /// CampaignCreatedApplicationEvent is (re)delivered, since outbox delivery ordering across
    /// separate outbox rows is not guaranteed.</summary>
    Task SetActiveStatusAsync(Guid campaignId, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken);

    /// <summary>Reads the current replicated snapshot for a campaign, or null if none has been
    /// replicated yet (the CampaignCreatedApplicationEvent hasn't been processed).</summary>
    Task<ActiveCampaignSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken);
}
