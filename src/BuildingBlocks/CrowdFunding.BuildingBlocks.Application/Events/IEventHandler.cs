namespace CrowdFunding.BuildingBlocks.Application.Events;

public interface IEventHandler<in TEvent>
    where TEvent : class
{
    Task Handle(TEvent notification, CancellationToken cancellationToken);
}