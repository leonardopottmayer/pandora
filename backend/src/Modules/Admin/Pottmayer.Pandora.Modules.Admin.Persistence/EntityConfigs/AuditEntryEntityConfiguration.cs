using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.EntityConfigs;

internal sealed class AuditEntryEntityConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("adm006_audit_entry", AdminModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_adm006");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(a => a.TargetType).HasColumnName("target_type").HasMaxLength(30).IsRequired();
        builder.Property(a => a.TargetId).HasColumnName("target_id").HasMaxLength(150).IsRequired();
        builder.Property(a => a.Before).HasColumnName("before").HasColumnType("jsonb");
        builder.Property(a => a.After).HasColumnName("after").HasColumnType("jsonb");
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();
    }
}
