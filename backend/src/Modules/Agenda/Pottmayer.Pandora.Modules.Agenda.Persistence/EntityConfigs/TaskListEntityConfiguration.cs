using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class TaskListEntityConfiguration : IEntityTypeConfiguration<TaskList>
{
    public void Configure(EntityTypeBuilder<TaskList> builder)
    {
        builder.ToTable("agd004_task_list", AgendaModule.Schema);

        builder.HasKey(l => l.Id).HasName("pk_agd004");
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(l => l.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(l => l.Position).HasColumnName("position").IsRequired();
        builder.Property(l => l.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(l => l.UserId).HasDatabaseName("ix_agd004_user_id");
        builder.HasIndex(l => l.UserId)
               .IsUnique()
               .HasFilter("is_default")
               .HasDatabaseName("uq_agd004_user_default");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
    }
}
