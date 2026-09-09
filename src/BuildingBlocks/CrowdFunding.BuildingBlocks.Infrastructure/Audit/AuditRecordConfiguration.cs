using CrowdFunding.BuildingBlocks.Application.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// Configures EF Core persistence for <see cref="AuditRecord"/>: table <c>system.audit_records</c>.
/// </summary>
public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records", "system");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(x => x.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(x => x.ActorEmail).HasColumnName("actor_email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CommandType).HasColumnName("command_type").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512).IsRequired();
        builder.Property(x => x.TimestampUtc).HasColumnName("timestamp_utc").IsRequired();

        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => x.TimestampUtc);
    }
}
