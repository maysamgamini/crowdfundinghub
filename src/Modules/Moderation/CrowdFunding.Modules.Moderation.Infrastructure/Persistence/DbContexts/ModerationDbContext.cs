using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Represents the EF Core database context for the moderation area.
/// </summary>
public sealed class ModerationDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL schema this module owns. Every table of the module (aggregates, read models,
    /// outbox, dead letters, inbox) and its migrations history live here; no other module maps a
    /// table into it. See docs/data-ownership.md.
    /// </summary>
    public const string Schema = "moderation";

    public ModerationDbContext(DbContextOptions<ModerationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CampaignReview> CampaignReviews => Set<CampaignReview>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModerationDbContext).Assembly);
        modelBuilder.ConfigureOutbox("moderation_outbox_messages");
        modelBuilder.ConfigureDeadLetter("moderation_dead_letter_events");
        base.OnModelCreating(modelBuilder);
    }
}
