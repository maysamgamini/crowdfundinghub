using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Configurations;

public sealed class CampaignOwnerCacheConfiguration : IEntityTypeConfiguration<CampaignOwnerCache>
{
    public void Configure(EntityTypeBuilder<CampaignOwnerCache> builder)
    {
        builder.ToTable("campaign_owner_cache");
        builder.HasKey(x => x.CampaignId);
        builder.Property(x => x.CampaignId).ValueGeneratedNever();
        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
