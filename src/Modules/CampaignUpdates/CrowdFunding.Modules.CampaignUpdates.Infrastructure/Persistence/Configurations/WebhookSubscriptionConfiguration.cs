using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Configurations;

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CampaignId).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SecretKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.ConsecutiveFailureCount).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.CampaignId, x.IsActive });
    }
}
