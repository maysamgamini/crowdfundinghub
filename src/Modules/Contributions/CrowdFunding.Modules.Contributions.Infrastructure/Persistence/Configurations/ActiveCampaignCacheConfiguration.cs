using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for the replicated <see cref="ActiveCampaignCache"/> read model.
/// </summary>
public sealed class ActiveCampaignCacheConfiguration : IEntityTypeConfiguration<ActiveCampaignCache>
{
    public void Configure(EntityTypeBuilder<ActiveCampaignCache> builder)
    {
        builder.ToTable("active_campaigns_cache");

        builder.HasKey(x => x.CampaignId);
        builder.Property(x => x.CampaignId).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.DeadlineUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.CampaignId, x.IsActive })
            .HasDatabaseName("ix_active_campaigns_cache_lookup");
    }
}
