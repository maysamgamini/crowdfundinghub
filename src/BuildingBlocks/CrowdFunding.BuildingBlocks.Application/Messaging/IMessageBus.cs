namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// The single boundary every outbox processor publishes through, decoupled from whether the
/// event ends up dispatched in-process (the monolith's default) or on a distributed broker.
/// Application event handlers never see this type — they implement
/// <see cref="CrowdFunding.BuildingBlocks.Application.Events.IEventHandler{TEvent}"/> exactly the
/// same way regardless of which <see cref="IMessageBus"/> implementation is active — so flipping
/// <c>Messaging:Provider</c> from <c>"InProcess"</c> to <c>"RabbitMQ"</c> in configuration is the
/// entire migration cost of extracting a module into an independently deployed service.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a single application event, wrapping it in a CloudEvents v1.0 envelope when the
    /// active provider crosses a process boundary (e.g. RabbitMQ); a no-op wrapper for the
    /// in-process provider, since local handlers receive the event object directly.
    /// </summary>
    Task PublishAsync(object applicationEvent, CancellationToken cancellationToken = default);
}
