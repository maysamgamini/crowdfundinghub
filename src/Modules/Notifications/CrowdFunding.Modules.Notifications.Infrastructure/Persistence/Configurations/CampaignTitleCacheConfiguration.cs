using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for the replicated <see cref="CampaignTitleCache"/> read model.
/// </summary>
public sealed class CampaignTitleCacheConfiguration : IEntityTypeConfiguration<CampaignTitleCache>
{
    public void Configure(EntityTypeBuilder<CampaignTitleCache> builder)
    {
        builder.ToTable("campaign_title_cache");

        builder.HasKey(x => x.CampaignId);
        builder.Property(x => x.CampaignId).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
