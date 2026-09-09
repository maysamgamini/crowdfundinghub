using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for Contribution.
/// </summary>
public sealed class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    /// <summary>
    /// Configures the EF Core mapping and constraints for <see cref="Contribution"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.ToTable("contributions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CampaignId)
            .IsRequired();

        builder.Property(x => x.ContributorId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PaymentReference)
            .HasMaxLength(100);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.Property(x => x.ExternalPaymentIntentId)
            .HasMaxLength(100);

        builder.Property(x => x.PaymentGateway)
            .HasMaxLength(50);

        // The webhook reconciliation handler's correlation lookup (TICKET-033) — a payment
        // gateway webhook arrives keyed by its own intent id, never by our internal ContributionId.
        builder.HasIndex(x => x.ExternalPaymentIntentId)
            .IsUnique()
            .HasFilter("\"ExternalPaymentIntentId\" IS NOT NULL");

        builder.OwnsOne(x => x.Money, money =>
        {
            money.Property(x => x.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(x => x.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc);

        builder.Property(x => x.RewardTierReservationId);

        // The hot read path (GET /api/campaigns/{campaignId}/contributions, and any per-backer
        // lookup) filters on these columns; without an index Postgres does a sequential scan of
        // the whole table on every request as pledge volume grows.
        builder.HasIndex(x => x.ContributorId);
        builder.HasIndex(x => new { x.CampaignId, x.CreatedAtUtc });

        // Optimistic concurrency token (mirrors CampaignConfiguration). Without it, a payment
        // confirmation and a payment failure racing on the same Pending contribution silently
        // overwrite each other's state instead of one of them failing with a detectable
        // conflict — e.g. a contribution already marked Succeeded (with
        // ContributionPaymentConfirmedDomainEvent already in the outbox) could be clobbered back
        // to Failed by a losing concurrent write.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
