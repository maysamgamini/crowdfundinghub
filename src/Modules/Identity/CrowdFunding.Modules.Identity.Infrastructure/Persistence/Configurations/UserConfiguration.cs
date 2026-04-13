using CrowdFunding.Modules.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures EF Core persistence for User.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.OwnsMany(x => x.Roles, roles =>
        {
            roles.ToTable("user_roles");
            roles.WithOwner().HasForeignKey("user_id");
            roles.HasKey("Id");

            roles.Property(x => x.Id)
                .ValueGeneratedNever();

            roles.Property(x => x.Role)
                .HasColumnName("role")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.OwnsMany(x => x.Permissions, permissions =>
        {
            permissions.ToTable("user_permissions");
            permissions.WithOwner().HasForeignKey("user_id");
            permissions.HasKey("Id");

            permissions.Property(x => x.Id)
                .ValueGeneratedNever();

            permissions.Property(x => x.Permission)
                .HasColumnName("permission")
                .HasMaxLength(100)
                .IsRequired();
        });
    }
}
