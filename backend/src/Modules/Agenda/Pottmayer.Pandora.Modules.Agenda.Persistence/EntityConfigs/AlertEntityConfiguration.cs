using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class AlertEntityConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("agd007_alert", AgendaModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_agd007");
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.SubjectType).HasColumnName("subject_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(a => a.OffsetMinutes).HasColumnName("offset_minutes").IsRequired();
        builder.Property(a => a.Channels).HasColumnName("channels").HasColumnType("text[]");
        builder.Property(a => a.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_agd007_user_id");
        builder.HasIndex(a => new { a.SubjectType, a.SubjectId }).HasDatabaseName("ix_agd007_subject");
        builder.HasIndex(a => a.SubjectType)
               .HasFilter("is_enabled")
               .HasDatabaseName("ix_agd007_enabled_subject_type");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
    }
}
