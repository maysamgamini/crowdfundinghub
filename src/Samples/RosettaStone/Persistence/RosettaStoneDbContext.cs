using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Samples.RosettaStone.Persistence;

/// <summary>
/// Isolated <c>rosetta</c>-schema `DbContext` backing Tier 1 and Tier 2 of the Rosetta Stone
/// sample. Kept separate from <c>CampaignsDbContext</c> on purpose: a Minimal API or pragmatic
/// CQRS slice that owns its own schema is exactly as extractable into a standalone microservice
/// as the Rich DDD Tier 3 pipeline is — decomposition-readiness is a property of schema
/// ownership, not of how many files or how much ceremony sit in front of the write.
/// </summary>
public sealed class RosettaStoneDbContext : DbContext
{
    public RosettaStoneDbContext(DbContextOptions<RosettaStoneDbContext> options) : base(options) { }

    public DbSet<CampaignRecord> CampaignRecords => Set<CampaignRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rosetta");

        modelBuilder.Entity<CampaignRecord>(builder =>
        {
            builder.ToTable("campaign_records");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Title).HasMaxLength(200).IsRequired();
            builder.Property(record => record.Story).HasMaxLength(5000).IsRequired();
            builder.Property(record => record.TargetAmount).HasColumnType("numeric(18,2)");
            builder.Property(record => record.Currency).HasMaxLength(3).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
