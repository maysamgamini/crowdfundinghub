using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for ContributionLedgerEntry.
/// </summary>
public sealed class ContributionLedgerEntryConfiguration : IEntityTypeConfiguration<ContributionLedgerEntry>
{
    /// <summary>
    /// Configures EF Core mapping and constraints for <see cref="ContributionLedgerEntry"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ContributionLedgerEntry> builder)
    {
        builder.ToTable("campaign_contributions_ledger");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CampaignId)
            .IsRequired();

        builder.Property(x => x.ContributionId)
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
        // safely because a duplicate contribution_id violates this constraint (SQLSTATE 23505)
        // instead of double-crediting RaisedAmount.
        builder.HasIndex(x => x.ContributionId)
            .IsUnique()
            .HasDatabaseName("uq_campaign_contributions_ledger_contribution_id");
    }
}
