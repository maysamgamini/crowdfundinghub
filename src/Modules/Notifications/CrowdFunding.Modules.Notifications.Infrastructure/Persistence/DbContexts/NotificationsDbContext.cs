using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Notifications' own isolated schema. Deliberately carries no outbox — Notifications is a pure
/// event sink (it consumes other modules' application events, it never produces one of its own),
/// so there is nothing for it to publish reliably. Its only persistence need is the replicated
/// <see cref="CampaignTitleCache"/> read model. See TICKET-031.
/// </summary>
public sealed class NotificationsDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL schema this module owns. Every table of the module (aggregates, read models,
    /// outbox, dead letters, inbox) and its migrations history live here; no other module maps a
    /// table into it. See docs/data-ownership.md.
    /// </summary>
    public const string Schema = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<CampaignTitleCache> CampaignTitleCache => Set<CampaignTitleCache>();
    public DbSet<CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference> NotificationPreferences => Set<CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
