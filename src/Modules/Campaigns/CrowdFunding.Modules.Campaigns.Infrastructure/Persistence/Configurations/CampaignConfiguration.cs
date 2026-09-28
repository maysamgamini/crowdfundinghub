using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for Campaign.
/// </summary>
public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    /// <summary>
    /// Configures EF Core mapping and constraints for <see cref="Campaign"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.OwnerId)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Story)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DeadlineUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.OwnsOne(x => x.GoalAmount, money =>
        {
            money.Property(x => x.Amount)
                .HasColumnName("goal_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(x => x.Currency)
                .HasColumnName("goal_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // RaisedAmount is a snapshot of SUM(campaign_ledger_entries.Amount), written only by
        // CampaignLedger's recompute statement. EF inserts it once (zero) and never updates it
        // (after-save behavior Ignore), so no code path that saves a Campaign can overwrite the
        // ledger's total with a stale or hand-computed value.
        builder.OwnsOne(x => x.RaisedAmount, money =>
        {
            money.Property(x => x.Amount)
                .HasColumnName("raised_amount")
                .HasPrecision(18, 2)
                .IsRequired()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            money.Property(x => x.Currency)
                .HasColumnName("raised_currency")
                .HasMaxLength(3)
                .IsRequired()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        });

        // PostgreSQL system column used as the row version. Every campaign writer is serialized
        // by the per-campaign advisory lock and reloads inside it, so this token is a safety net:
        // a write that bypasses the lock with a stale row fails (DbUpdateConcurrencyException)
        // instead of silently overwriting status or deadline changes. The ledger's snapshot
        // update also bumps it, so a stale lifecycle write cannot race a pledge either.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
