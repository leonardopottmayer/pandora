using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class CalendarEntityConfiguration : IEntityTypeConfiguration<Calendar>
{
    public void Configure(EntityTypeBuilder<Calendar> builder)
    {
        builder.ToTable("agd001_calendar", AgendaModule.Schema);

        builder.HasKey(c => c.Id).HasName("pk_agd001");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Color).HasColumnName("color").HasMaxLength(50);
        builder.Property(c => c.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(c => c.IsVisible).HasColumnName("is_visible").IsRequired();
        builder.Property(c => c.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Origin).HasColumnName("origin").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(c => c.UserId).HasDatabaseName("ix_agd001_user_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
    }
}
