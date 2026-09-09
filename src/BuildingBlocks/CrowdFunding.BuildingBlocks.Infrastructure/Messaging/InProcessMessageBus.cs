using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// The monolith's default <see cref="IMessageBus"/>: dispatches events directly to in-process
/// <see cref="IEventHandler{TEvent}"/> implementations via <see cref="IEventPublisher"/>, with
/// zero serialization, network I/O, or extra infrastructure. This is what every module runs
/// against locally and in the reference deployment — switching to <see cref="RabbitMqMessageBus"/>
/// is a configuration change (<c>Messaging:Provider = "RabbitMQ"</c>), not a code change, on
/// either the publishing or the handling side.
/// </summary>
public sealed class InProcessMessageBus : IMessageBus
{
    private readonly IEventPublisher _eventPublisher;

    public InProcessMessageBus(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public Task PublishAsync(object applicationEvent, CancellationToken cancellationToken = default)
        => _eventPublisher.PublishAsync(applicationEvent, cancellationToken);
}
