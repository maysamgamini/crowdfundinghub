namespace CrowdFunding.BuildingBlocks.Application.Events;

public interface IEventPublisher
{
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);
}