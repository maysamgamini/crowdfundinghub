using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;

public sealed class CampaignsDbContext : DbContext
{
    public CampaignsDbContext(DbContextOptions<CampaignsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}