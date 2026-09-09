using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the campaigns area.
/// </summary>
public sealed class CampaignsDbContext : DbContext
{
    public CampaignsDbContext(DbContextOptions<CampaignsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<RewardTier> RewardTiers => Set<RewardTier>();
    public DbSet<RewardTierReservation> RewardTierReservations => Set<RewardTierReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ContributionLedgerEntry> ContributionLedgerEntries => Set<ContributionLedgerEntry>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampaignsDbContext).Assembly);
        modelBuilder.ConfigureOutbox("campaigns_outbox_messages");
        modelBuilder.ConfigureDeadLetter("campaigns_dead_letter_events");
        base.OnModelCreating(modelBuilder);
    }
}
