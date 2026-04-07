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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContributionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
