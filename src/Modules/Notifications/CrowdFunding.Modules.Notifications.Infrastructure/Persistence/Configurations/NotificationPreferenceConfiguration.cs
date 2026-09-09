using CrowdFunding.Modules.Notifications.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for the <see cref="NotificationPreference"/> aggregate.
/// </summary>
public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();

        builder.Property(x => x.CampaignUpdatesEnabled).IsRequired();
        builder.Property(x => x.MarketingAnnouncementsEnabled).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
