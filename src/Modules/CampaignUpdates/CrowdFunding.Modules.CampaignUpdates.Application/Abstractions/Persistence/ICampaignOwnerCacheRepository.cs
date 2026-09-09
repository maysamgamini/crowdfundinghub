namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

/// <summary>A read-only snapshot of a campaign's owner, replicated locally (Event-Carried State
/// Transfer) so registering a webhook subscription can enforce ownership without a synchronous
/// cross-module call into Campaigns. See TICKET-023 / TICKET-035.</summary>
public sealed record CampaignOwnerSnapshot(Guid CampaignId, Guid OwnerId);

public interface ICampaignOwnerCacheRepository
{
    Task UpsertAsync(Guid campaignId, Guid ownerId, DateTime updatedAtUtc, CancellationToken cancellationToken);

    Task<CampaignOwnerSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken);
}
