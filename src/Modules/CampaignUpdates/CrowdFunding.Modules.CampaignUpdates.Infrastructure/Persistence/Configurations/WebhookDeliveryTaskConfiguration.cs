using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryTaskConfiguration : IEntityTypeConfiguration<WebhookDeliveryTask>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryTask> builder)
    {
        builder.ToTable("webhook_delivery_tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SubscriptionId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.ScheduledAtUtc).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.WorkerId).HasMaxLength(100);
        builder.Property(x => x.LockedUntilUtc);

        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
    }
}
