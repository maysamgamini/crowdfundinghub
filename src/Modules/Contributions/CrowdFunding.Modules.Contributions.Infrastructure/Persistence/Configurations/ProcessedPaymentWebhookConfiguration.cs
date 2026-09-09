using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for the <see cref="ProcessedPaymentWebhook"/> idempotency store.
/// </summary>
public sealed class ProcessedPaymentWebhookConfiguration : IEntityTypeConfiguration<ProcessedPaymentWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedPaymentWebhook> builder)
    {
        builder.ToTable("processed_payment_webhooks");

        builder.HasKey(x => x.WebhookEventId);
        builder.Property(x => x.WebhookEventId).HasMaxLength(100).ValueGeneratedNever();

        builder.Property(x => x.PaymentIntentId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProcessedAtUtc).IsRequired();

        builder.HasIndex(x => x.PaymentIntentId);
    }
}
