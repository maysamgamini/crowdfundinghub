using System.ComponentModel.DataAnnotations.Schema;

namespace CrowdFunding.BuildingBlocks.Domain.Common;

/// <summary>
/// Provides shared domain-event support for aggregate roots and entities.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<BaseEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Clears all recorded domain events from the entity.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Records a new domain event to be dispatched upon successful transaction commit.
    /// </summary>
    /// <param name="domainEvent">The domain event to append.</param>
    protected void AddDomainEvent(BaseEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes a previously recorded domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    protected void RemoveDomainEvent(BaseEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }
}
