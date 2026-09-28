using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Builds the schema-qualified, quoted table name of an entity from the EF Core model, for the few
/// statements that must be written as raw SQL (the outbox claim, the inbox claim).
/// </summary>
/// <remarks>
/// Every module maps its tables into its own PostgreSQL schema (<c>HasDefaultSchema</c>), so an
/// unqualified name in raw SQL would resolve through <c>search_path</c> (normally <c>public</c>)
/// and miss the table. Reading the name from the model instead of a hand-written string means the
/// SQL follows the mapping: there is no second copy of the table name to drift.
/// </remarks>
public static class QualifiedTableName
{
    /// <summary>
    /// Returns <c>"schema"."table"</c> (or <c>"table"</c> when the entity has no schema) for
    /// <typeparamref name="TEntity"/> as mapped by <paramref name="dbContext"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="dbContext">The DbContext whose model maps the entity.</param>
    /// <returns>The quoted, schema-qualified table name, safe to splice into SQL text.</returns>
    /// <exception cref="InvalidOperationException">The DbContext does not map the entity to a table.</exception>
    public static string Of<TEntity>(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
                         ?? throw new InvalidOperationException(
                             $"{dbContext.GetType().Name} does not map {typeof(TEntity).Name}.");

        var table = entityType.GetTableName()
                    ?? throw new InvalidOperationException(
                        $"{typeof(TEntity).Name} is not mapped to a table in {dbContext.GetType().Name}.");
        var schema = entityType.GetSchema();

        // Identifiers come from the compiled EF model, never from input; quoting them keeps the
        // exact case and makes the statement independent of search_path.
        return schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
    }
}
