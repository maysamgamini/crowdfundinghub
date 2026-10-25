using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

public sealed class RefundJobConfiguration : IEntityTypeConfiguration<RefundJob>
{
    public void Configure(EntityTypeBuilder<RefundJob> builder)
    {
        builder.ToTable("refund_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The guardrail: one job per campaign, whatever code path or redelivered event tries a second.
        builder.HasIndex(x => x.CampaignId)
            .IsUnique()
            .HasDatabaseName(PledgeIndexes.OneRefundJobPerCampaign);

        // The worker reads what is still open; finished jobs are never looked at again.
        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("IX_refund_jobs_open")
            .HasFilter("\"Status\" = 'Open'");

        // Two workers finishing the same job: the second save fails instead of overwriting the first.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
