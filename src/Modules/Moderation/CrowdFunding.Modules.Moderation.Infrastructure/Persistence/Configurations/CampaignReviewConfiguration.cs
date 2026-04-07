using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Configurations;

public sealed class CampaignReviewConfiguration : IEntityTypeConfiguration<CampaignReview>
{
    public void Configure(EntityTypeBuilder<CampaignReview> builder)
    {
        builder.ToTable("campaign_reviews");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CampaignId)
            .IsRequired();

        builder.HasIndex(x => x.CampaignId)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();
    }
}
