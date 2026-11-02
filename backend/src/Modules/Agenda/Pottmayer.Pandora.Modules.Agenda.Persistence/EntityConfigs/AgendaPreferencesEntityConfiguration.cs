using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class AgendaPreferencesEntityConfiguration : IEntityTypeConfiguration<AgendaPreferences>
{
    public void Configure(EntityTypeBuilder<AgendaPreferences> builder)
    {
        builder.ToTable("agd013_preferences", AgendaModule.Schema);

        builder.HasKey(p => p.UserId).HasName("pk_agd013");

        builder.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(p => p.DefaultAlertOffsetMinutes).HasColumnName("default_alert_offset_minutes").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
    }
}
