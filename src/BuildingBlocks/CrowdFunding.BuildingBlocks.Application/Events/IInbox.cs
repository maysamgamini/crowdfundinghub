namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// A module's record of which (message, consumer) pairs it has already handled. The outbox
/// delivers at least once, so when one handler of an event succeeds and another throws, the whole
/// message is retried; the inbox lets the retry skip the handlers that already finished.
/// </summary>
public interface IInbox
{
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, string consumer, DateTime processedAtUtc, CancellationToken cancellationToken);
}
