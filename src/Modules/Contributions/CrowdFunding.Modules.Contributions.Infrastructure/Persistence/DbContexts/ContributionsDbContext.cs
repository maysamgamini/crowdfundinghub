using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the contributions area.
/// </summary>
public sealed class ContributionsDbContext : DbContext
{
    public ContributionsDbContext(DbContextOptions<ContributionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<ActiveCampaignCache> ActiveCampaignsCache => Set<ActiveCampaignCache>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContributionsDbContext).Assembly);
        modelBuilder.ConfigureOutbox("contributions_outbox_messages");
        modelBuilder.ConfigureDeadLetter("contributions_dead_letter_events");
        base.OnModelCreating(modelBuilder);
    }
}
