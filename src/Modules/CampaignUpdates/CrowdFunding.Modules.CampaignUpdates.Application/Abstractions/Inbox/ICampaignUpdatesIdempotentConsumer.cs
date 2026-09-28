using CrowdFunding.BuildingBlocks.Application.Inbox;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Inbox;

/// <summary>
/// The CampaignUpdates module's <see cref="IIdempotentConsumer"/>, backed by
/// <c>campaign_updates_processed_events</c> so the processed-event record commits in the same
/// transaction as the webhook delivery tasks it guards.
/// </summary>
public interface ICampaignUpdatesIdempotentConsumer : IIdempotentConsumer;
