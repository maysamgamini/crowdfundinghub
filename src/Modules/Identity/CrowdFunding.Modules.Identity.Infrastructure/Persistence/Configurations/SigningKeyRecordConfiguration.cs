using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class SigningKeyRecordConfiguration : IEntityTypeConfiguration<SigningKeyRecord>
{
    public void Configure(EntityTypeBuilder<SigningKeyRecord> builder)
    {
        builder.ToTable("identity_signing_keys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kid).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PrivateKeyPkcs8Base64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasIndex(x => x.Kid).IsUnique();
    }
}
