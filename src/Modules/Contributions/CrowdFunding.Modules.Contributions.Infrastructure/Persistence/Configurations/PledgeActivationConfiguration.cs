using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Configurations;

public sealed class PledgeActivationConfiguration : IEntityTypeConfiguration<PledgeActivation>
{
    public void Configure(EntityTypeBuilder<PledgeActivation> builder)
    {
        builder.ToTable("pledge_activations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentMethodReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ActivatedAtUtc).IsRequired();

        // The guardrail: one activation per pledge, whatever code path or racing request tries a second.
        // The name is a constant so the transaction executor can recognise this violation and nothing else.
        builder.HasIndex(x => x.ContributionId)
            .IsUnique()
            .HasDatabaseName(ContributionIndexes.OneActivationPerPledge);

        // A foreign key keeps an activation from pointing at a pledge that does not exist.
        builder.HasOne<Contribution>().WithMany().HasForeignKey(x => x.ContributionId).OnDelete(DeleteBehavior.Cascade);
    }
}
