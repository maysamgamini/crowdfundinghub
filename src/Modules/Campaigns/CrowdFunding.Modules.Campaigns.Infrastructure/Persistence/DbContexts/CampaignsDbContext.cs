using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the campaigns area.
/// </summary>
public sealed class CampaignsDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL schema this module owns. Every table of the module (aggregates, read models,
    /// outbox, dead letters, inbox) and its migrations history live here; no other module maps a
    /// table into it. See docs/data-ownership.md.
    /// </summary>
    public const string Schema = "campaigns";

    public CampaignsDbContext(DbContextOptions<CampaignsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<RewardTier> RewardTiers => Set<RewardTier>();
    public DbSet<RewardTierReservation> RewardTierReservations => Set<RewardTierReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<CampaignLedgerEntry> LedgerEntries => Set<CampaignLedgerEntry>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignsDbContext).Assembly);
        modelBuilder.ConfigureOutbox("campaigns_outbox_messages");
        modelBuilder.ConfigureDeadLetter("campaigns_dead_letter_events");
        base.OnModelCreating(modelBuilder);
    }
}
