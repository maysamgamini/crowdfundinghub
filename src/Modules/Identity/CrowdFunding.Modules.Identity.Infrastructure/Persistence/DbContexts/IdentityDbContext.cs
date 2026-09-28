using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Domain.Entities;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the identity area.
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL schema this module owns. Every table of the module (aggregates, read models,
    /// outbox, dead letters, inbox) and its migrations history live here; no other module maps a
    /// table into it. See docs/data-ownership.md.
    /// </summary>
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<SigningKeyRecord> SigningKeys => Set<SigningKeyRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
