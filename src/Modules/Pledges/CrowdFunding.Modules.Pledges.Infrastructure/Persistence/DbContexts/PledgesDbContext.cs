using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the pledges area.
/// </summary>
public sealed class PledgesDbContext : DbContext
{
    public PledgesDbContext(DbContextOptions<PledgesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Pledge> Pledges => Set<Pledge>();
    public DbSet<ChargeAttempt> ChargeAttempts => Set<ChargeAttempt>();

    public DbSet<PledgeActivation> PledgeActivations => Set<PledgeActivation>();
    public DbSet<ActiveCampaignCache> ActiveCampaignsCache => Set<ActiveCampaignCache>();
    public DbSet<ProcessedPaymentWebhook> ProcessedPaymentWebhooks => Set<ProcessedPaymentWebhook>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every table of this module lives in its own PostgreSQL schema.
        modelBuilder.HasDefaultSchema("pledges");
        modelBuilder.ConfigureInbox("pledges_inbox_messages");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PledgesDbContext).Assembly);
        modelBuilder.ConfigureOutbox("pledges_outbox_messages");
        modelBuilder.ConfigureDeadLetter("pledges_dead_letter_events");
        base.OnModelCreating(modelBuilder);
    }
}
