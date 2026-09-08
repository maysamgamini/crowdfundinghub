using CrowdFunding.BuildingBlocks.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Reads and clears domain events from tracked entities during persistence.
/// </summary>
public static class DomainEventAccessor
{
    /// <summary>
    /// Extracts all uncommitted domain events from tracked <see cref="BaseEntity"/> instances in the DbContext.
    /// </summary>
    /// <param name="dbContext">The database context whose change tracker is inspected.</param>
    /// <returns>A read-only collection of all extracted domain events.</returns>
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

    /// <summary>
    /// Clears all recorded domain events across all tracked entities in the DbContext change tracker.
    /// </summary>
    /// <param name="dbContext">The database context whose change tracker entities will have events cleared.</param>
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
