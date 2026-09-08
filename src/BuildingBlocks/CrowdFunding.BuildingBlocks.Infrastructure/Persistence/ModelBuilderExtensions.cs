using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Adds shared EF Core model configuration used by module DbContexts.
/// </summary>
public static class ModelBuilderExtensions
{
    public static ModelBuilder ConfigureOutbox(this ModelBuilder modelBuilder, string tableName)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).HasMaxLength(512).IsRequired();
            builder.Property(x => x.Version).IsRequired();
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.Error).HasMaxLength(4000);
            builder.Property(x => x.Attempts).HasDefaultValue(0);
            builder.Property(x => x.Status).HasConversion<int>().IsRequired();
            builder.Property(x => x.ScheduledAtUtc).IsRequired();
            builder.Property(x => x.LockedBy).HasMaxLength(100);

            // Partial index: only Pending rows are ever queried by the claim statement, so the
            // index stays small and cheap to scan regardless of how many Processed/DeadLetter
            // rows accumulate in the table over time.
            builder.HasIndex(x => new { x.ScheduledAtUtc, x.Id })
                .HasFilter($"\"{nameof(OutboxMessage.Status)}\" = {(int)OutboxMessageStatus.Pending}")
                .HasDatabaseName($"ix_{tableName}_pending");
        });

        return modelBuilder;
    }

    public static ModelBuilder ConfigureDeadLetter(this ModelBuilder modelBuilder, string tableName)
    {
        modelBuilder.Entity<DeadLetterEvent>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).HasMaxLength(512).IsRequired();
            builder.Property(x => x.Version).IsRequired();
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.FailureReason).HasMaxLength(4000).IsRequired();
            builder.Property(x => x.RecordedAtUtc).IsRequired();
            builder.HasIndex(x => x.SourceEventId);
        });

        return modelBuilder;
    }
}
