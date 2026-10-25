using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for PledgeLedgerEntry.
/// </summary>
public sealed class PledgeLedgerEntryConfiguration : IEntityTypeConfiguration<PledgeLedgerEntry>
{
    /// <summary>
    /// Configures EF Core mapping and constraints for <see cref="PledgeLedgerEntry"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<PledgeLedgerEntry> builder)
    {
        // The sign belongs to the kind: a pledge adds money (positive), a refund takes it back (negative).
        // The database refuses anything else, so a wrongly signed entry cannot be written by any code path.
        builder.ToTable("campaign_pledges_ledger", table => table.HasCheckConstraint(
            "CK_campaign_pledges_ledger_sign_matches_kind",
            "(\"Kind\" = 'Pledge' AND \"Amount\" > 0) OR (\"Kind\" = 'Refund' AND \"Amount\" < 0)"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CampaignId)
            .IsRequired();

        builder.Property(x => x.PledgeId)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.RecordedAtUtc)
            .IsRequired();

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency guard: at-least-once delivery of the payment-confirmed event can retry
        // safely because a duplicate pledge_id violates this constraint (SQLSTATE 23505)
        // instead of double-crediting RaisedAmount.
        builder.HasIndex(x => new { x.PledgeId, x.Kind })
            .IsUnique()
            .HasDatabaseName("uq_campaign_pledges_ledger_pledge_kind");
    }
}
