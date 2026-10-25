using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

public sealed class ChargeAttemptConfiguration : IEntityTypeConfiguration<ChargeAttempt>
{
    public void Configure(EntityTypeBuilder<ChargeAttempt> builder)
    {
        builder.ToTable("charge_attempts");

        builder.HasKey(x => new { x.PledgeId, x.AttemptNumber });

        builder.Property(x => x.ProviderKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(100);
        builder.Property(x => x.DeclineCode).HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The key sent to the provider names one attempt of one pledge, so it is unique.
        builder.HasIndex(x => x.ProviderKey).IsUnique();

        // At most one successful attempt per pledge: the database refuses a second success even if a bug or a
        // race tried to record one, which is the last line of defence against charging a backer twice.
        builder.HasIndex(x => x.PledgeId)
            .IsUnique()
            .HasDatabaseName("ux_charge_attempts_one_success_per_pledge")
            .HasFilter("\"Status\" = 'Succeeded'");
    }
}
