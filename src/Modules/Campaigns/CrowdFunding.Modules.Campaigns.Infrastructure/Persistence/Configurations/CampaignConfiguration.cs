using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for Campaign.
/// </summary>
public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
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

        builder.OwnsOne(x => x.RaisedAmount, money =>
        {
            money.Property(x => x.Amount)
                .HasColumnName("raised_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(x => x.Currency)
                .HasColumnName("raised_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // PostgreSQL system column used as an optimistic concurrency token. Prevents lost
        // updates when two concurrent confirmed contributions race to update RaisedAmount:
        // EF issues WHERE id = @p0 AND xmin = @p1, and a stale write throws
        // DbUpdateConcurrencyException instead of silently overwriting the other update.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
