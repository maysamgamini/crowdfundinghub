using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

public sealed class RefundAttemptConfiguration : IEntityTypeConfiguration<RefundAttempt>
{
    public void Configure(EntityTypeBuilder<RefundAttempt> builder)
    {
        builder.ToTable("refund_attempts", table => table.HasCheckConstraint(
            "CK_refund_attempts_failure_code_matches_outcome",
            "(\"Outcome\" = 'Failed' AND \"FailureCode\" IS NOT NULL) OR (\"Outcome\" = 'Succeeded' AND \"FailureCode\" IS NULL)"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(200);
        builder.Property(x => x.ProviderRefundId).HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The guardrail: an attempt number of a pledge is recorded once, so the keys of a pledge never repeat.
        builder.HasIndex(x => new { x.PledgeId, x.AttemptNumber })
            .IsUnique()
            .HasDatabaseName(PledgeIndexes.OneRefundAttemptNumberPerPledge);

        builder.HasOne<Pledge>().WithMany().HasForeignKey(x => x.PledgeId).OnDelete(DeleteBehavior.Restrict);
    }
}
