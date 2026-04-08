using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;

public sealed class ContributionsDbContext : DbContext
{
    public ContributionsDbContext(DbContextOptions<ContributionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContributionsDbContext).Assembly);
        modelBuilder.ConfigureOutbox();
        base.OnModelCreating(modelBuilder);
    }
}