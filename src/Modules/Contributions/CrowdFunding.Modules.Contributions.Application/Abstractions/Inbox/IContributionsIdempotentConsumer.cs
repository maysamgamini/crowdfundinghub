using CrowdFunding.BuildingBlocks.Application.Inbox;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Inbox;

/// <summary>
/// The Contributions module's <see cref="IIdempotentConsumer"/>, backed by
/// <c>contributions_processed_events</c> so the processed-event record commits in the same
/// transaction as the Contributions state change it guards.
/// </summary>
public interface IContributionsIdempotentConsumer : IIdempotentConsumer;
