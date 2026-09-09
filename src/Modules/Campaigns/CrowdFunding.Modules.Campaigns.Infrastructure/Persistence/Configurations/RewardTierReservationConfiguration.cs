using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for RewardTierReservation.
/// </summary>
public sealed class RewardTierReservationConfiguration : IEntityTypeConfiguration<RewardTierReservation>
{
    public void Configure(EntityTypeBuilder<RewardTierReservation> builder)
    {
        builder.ToTable("reward_tier_reservations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RewardTierId).IsRequired();
        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.BackerUserId).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.ContributionId);

        builder.HasIndex(x => x.RewardTierId);

        // The scavenger's poll query filters on these two columns together — see
        // RewardTierReservationRepository.GetExpiredBatchAsync.
        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
