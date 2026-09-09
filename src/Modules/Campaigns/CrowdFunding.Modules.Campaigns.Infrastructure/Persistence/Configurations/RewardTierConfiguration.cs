using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for RewardTier.
/// </summary>
public sealed class RewardTierConfiguration : IEntityTypeConfiguration<RewardTier>
{
    public void Configure(EntityTypeBuilder<RewardTier> builder)
    {
        builder.ToTable("reward_tiers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TotalCapacity).IsRequired();
        builder.Property(x => x.ClaimedCount).IsRequired();
        builder.Property(x => x.ReservedCount).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.OwnsOne(x => x.MinimumPledgeAmount, money =>
        {
            money.Property(x => x.Amount).HasColumnName("minimum_pledge_amount").HasPrecision(18, 2).IsRequired();
            money.Property(x => x.Currency).HasColumnName("minimum_pledge_currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(x => x.CampaignId);

        // xmin optimistic concurrency (mirrors CampaignConfiguration/ContributionConfiguration):
        // defense in depth underneath the advisory lock ReserveRewardTierSlotCommandHandler
        // already takes — belt-and-suspenders against any future write path that forgets to
        // acquire that lock.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
