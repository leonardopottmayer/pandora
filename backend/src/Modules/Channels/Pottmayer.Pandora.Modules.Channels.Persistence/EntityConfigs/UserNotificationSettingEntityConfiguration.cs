using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class UserNotificationSettingEntityConfiguration : IEntityTypeConfiguration<UserNotificationSetting>
{
    public void Configure(EntityTypeBuilder<UserNotificationSetting> builder)
    {
        builder.ToTable("chn007_user_notification_setting", ChannelsModule.Schema);

        builder.HasKey(s => s.Id)
               .HasName("pk_chn007");

        builder.Property(s => s.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(s => s.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(s => s.QuietHoursStart)
               .HasColumnName("quiet_hours_start")
               .HasColumnType("time");

        builder.Property(s => s.QuietHoursEnd)
               .HasColumnName("quiet_hours_end")
               .HasColumnType("time");

        builder.Property(s => s.QuietHoursBehaviour)
               .HasColumnName("quiet_hours_behaviour")
               .HasMaxLength(20)
               .HasConversion(
                   b => b == null ? null : b.Value,
                   v => v == null ? null : QuietHoursBehaviour.FromValue(v));

        builder.HasIndex(s => s.UserId)
               .HasDatabaseName("uq_chn007_user")
               .IsUnique();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
    }
}
