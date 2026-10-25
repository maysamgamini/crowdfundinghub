using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Configurations;

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
        builder.HasIndex(x => x.PledgeId)
            .IsUnique()
            .HasDatabaseName(PledgeIndexes.OneActivationPerPledge);

        // A foreign key keeps an activation from pointing at a pledge that does not exist.
        builder.HasOne<Pledge>().WithMany().HasForeignKey(x => x.PledgeId).OnDelete(DeleteBehavior.Cascade);
    }
}
