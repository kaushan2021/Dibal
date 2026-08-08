using Dibal.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dibal.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Before).HasColumnType("jsonb");
        builder.Property(a => a.After).HasColumnType("jsonb");

        builder.HasIndex(a => new { a.EntityName, a.EntityId, a.ChangedAt })
            .HasDatabaseName("ix_audit_entity")
            .IsDescending(false, false, true);

        builder.HasIndex(a => a.ChangedAt)
            .HasDatabaseName("ix_audit_changed_at")
            .IsDescending();
    }
}
