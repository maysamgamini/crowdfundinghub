using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

public sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts", table => table.HasCheckConstraint(
            PledgeIndexes.PayoutAmountsAddUp,
            "(\"GrossAmount\" IS NULL AND \"FeeAmount\" IS NULL AND \"NetAmount\" IS NULL) OR " +
            "(\"GrossAmount\" >= 0 AND \"FeeAmount\" >= 0 AND \"FeeAmount\" <= \"GrossAmount\" AND \"NetAmount\" = \"GrossAmount\" - \"FeeAmount\")"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.NetAmount).HasPrecision(18, 2);
        builder.Property(x => x.LastFailureCode).HasMaxLength(100);
        builder.Property(x => x.ProviderPayoutId).HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The guardrail: one payout per campaign, whatever code path or redelivered event tries a second.
        builder.HasIndex(x => x.CampaignId)
            .IsUnique()
            .HasDatabaseName(PledgeIndexes.OnePayoutPerCampaign);

        // The worker reads what is waiting; paid and empty payouts are never looked at again.
        builder.HasIndex(x => x.NextAttemptAtUtc)
            .HasDatabaseName("IX_payouts_open")
            .HasFilter("\"Status\" IN ('Pending', 'Ready')");

        // Two workers working on the same payout: the second save fails instead of overwriting the first.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
