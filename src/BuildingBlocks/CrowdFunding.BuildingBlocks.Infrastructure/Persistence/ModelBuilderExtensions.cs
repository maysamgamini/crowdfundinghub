using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    public static ModelBuilder ConfigureOutbox(this ModelBuilder modelBuilder, string tableName)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).HasMaxLength(2048).IsRequired();
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.Error).HasMaxLength(4000);
            builder.Property(x => x.Attempts).HasDefaultValue(0);
            builder.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc });
        });

        return modelBuilder;
    }
}
