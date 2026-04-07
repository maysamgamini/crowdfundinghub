using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;

public sealed class ModerationDbContext : DbContext
{
    public ModerationDbContext(DbContextOptions<ModerationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CampaignReview> CampaignReviews => Set<CampaignReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModerationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
