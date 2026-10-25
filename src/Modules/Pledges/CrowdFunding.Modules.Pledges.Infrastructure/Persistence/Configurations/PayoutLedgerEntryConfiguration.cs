using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

public sealed class PayoutLedgerEntryConfiguration : IEntityTypeConfiguration<PayoutLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PayoutLedgerEntry> builder)
    {
        builder.ToTable("payout_ledger_entries", table => table.HasCheckConstraint(
            PledgeIndexes.PayoutLedgerSignMatchesKind,
            "(\"Kind\" IN ('Charge', 'FeeReturned') AND \"Amount\" > 0) OR (\"Kind\" IN ('PlatformFee', 'PaidOut', 'ChargeRefunded') AND \"Amount\" < 0)"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The guardrails: a line about a pledge is written once per payout and kind, and so is a line about the payout.
        builder.HasIndex(x => new { x.PayoutId, x.Kind, x.PledgeId })
            .IsUnique()
            .HasFilter("\"PledgeId\" IS NOT NULL")
            .HasDatabaseName(PledgeIndexes.OnePayoutLedgerLinePerPledge);
        builder.HasIndex(x => new { x.PayoutId, x.Kind })
            .IsUnique()
            .HasFilter("\"PledgeId\" IS NULL")
            .HasDatabaseName(PledgeIndexes.OnePayoutLedgerLinePerPayout);

        builder.HasOne<Payout>().WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
    }
}
