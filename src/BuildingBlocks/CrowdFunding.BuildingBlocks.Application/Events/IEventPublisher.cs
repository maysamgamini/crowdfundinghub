namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Publishes application events to registered handlers.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);
}
