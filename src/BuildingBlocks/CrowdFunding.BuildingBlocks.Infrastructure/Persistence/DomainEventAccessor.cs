using CrowdFunding.BuildingBlocks.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Reads and clears domain events from tracked entities during persistence.
/// </summary>
public static class DomainEventAccessor
{
    public static IReadOnlyCollection<BaseEvent> GetDomainEvents(DbContext dbContext)
    {
        var entities = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();

        return entities
            .SelectMany(entity => entity.DomainEvents)
            .ToArray();
    }

    public static void ClearDomainEvents(DbContext dbContext)
    {
        var entities = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }
    }
}
