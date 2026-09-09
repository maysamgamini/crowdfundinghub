using CrowdFunding.BuildingBlocks.Application.Audit;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// The cross-cutting "system" schema's own DbContext (TICKET-039) — deliberately not owned by any
/// single module, since an audit trail spans all of them. <see cref="AuditRecord"/> (an immutable
/// record with only a positional constructor) is mapped directly rather than duplicating it as a
/// separate EF entity class; nothing here ever needs change-tracked mutation, only inserts.
/// </summary>
public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
