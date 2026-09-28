using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;

/// <summary>
/// CampaignUpdates' own isolated schema. Carries no generic outbox — webhook delivery has its
/// own durable, purpose-built queue (<see cref="WebhookDeliveryTask"/>) with its own backoff and
/// dead-letter semantics rather than reusing the cross-module application-event outbox, since
/// these deliveries never leave this module. See TICKET-035.
/// </summary>
public sealed class CampaignUpdatesDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL schema this module owns. Every table of the module (aggregates, read models,
    /// outbox, dead letters, inbox) and its migrations history live here; no other module maps a
    /// table into it. See docs/data-ownership.md.
    /// </summary>
    public const string Schema = "campaignupdates";

    public CampaignUpdatesDbContext(DbContextOptions<CampaignUpdatesDbContext> options) : base(options) { }

    public DbSet<CampaignOwnerCache> CampaignOwnerCache => Set<CampaignOwnerCache>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDeliveryTask> WebhookDeliveryTasks => Set<WebhookDeliveryTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignUpdatesDbContext).Assembly);
        modelBuilder.ConfigureInbox("campaign_updates_processed_events");
        base.OnModelCreating(modelBuilder);
    }
}
