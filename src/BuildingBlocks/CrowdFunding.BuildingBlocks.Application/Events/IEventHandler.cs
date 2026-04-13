namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Handles a specific application event.
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    Task Handle(TEvent notification, CancellationToken cancellationToken);
}
