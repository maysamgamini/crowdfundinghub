using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the append-only <c>campaign_ledger_entries</c> table. The constraints below are the
/// ledger's invariants, enforced by PostgreSQL for every writer, not by application code.
/// </summary>
public sealed class CampaignLedgerEntryConfiguration : IEntityTypeConfiguration<CampaignLedgerEntry>
{
    /// <summary>The ledger table name.</summary>
    public const string TableName = "campaign_ledger_entries";

    /// <summary>The unique index that makes crediting or refunding a contribution idempotent.</summary>
    public const string ContributionKindIndexName = "uq_campaign_ledger_entries_contribution_kind";

    /// <summary>
    /// Configures EF Core mapping and constraints for <see cref="CampaignLedgerEntry"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<CampaignLedgerEntry> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "ck_campaign_ledger_entries_kind_sign",
            // Pledges add, refunds subtract, and only the one-time opening balance has no contribution.
            $"""("Kind" = {(int)CampaignLedgerEntryKind.Pledge} AND "Amount" > 0 AND "ContributionId" IS NOT NULL) OR ("Kind" = {(int)CampaignLedgerEntryKind.Refund} AND "Amount" < 0 AND "ContributionId" IS NOT NULL) OR ("Kind" = {(int)CampaignLedgerEntryKind.OpeningBalance} AND "ContributionId" IS NULL)"""));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.RecordedAtUtc).IsRequired();

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency: a contribution can be credited once and reversed once. A redelivered
        // event hits this key and inserts nothing (ON CONFLICT DO NOTHING in CampaignLedger).
        builder.HasIndex(x => new { x.ContributionId, x.Kind })
            .IsUnique()
            .HasDatabaseName(ContributionKindIndexName);

        builder.HasIndex(x => x.CampaignId, "uq_campaign_ledger_entries_opening_balance")
            .IsUnique()
            .HasFilter($"\"Kind\" = {(int)CampaignLedgerEntryKind.OpeningBalance}");

        builder.HasIndex(x => x.CampaignId, "ix_campaign_ledger_entries_campaign_id");
    }
}
