using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures Entity Framework Core mapping, table conventions, and constraints for <see cref="SigningKeyRecord"/>.
/// </summary>
public sealed class SigningKeyRecordConfiguration : IEntityTypeConfiguration<SigningKeyRecord>
{
    /// <summary>
    /// Configures the entity properties, primary key, and unique index on <see cref="SigningKeyRecord.Kid"/>.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
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
