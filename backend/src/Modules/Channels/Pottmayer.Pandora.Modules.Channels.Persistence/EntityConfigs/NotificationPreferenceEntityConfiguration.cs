using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class NotificationPreferenceEntityConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("chn005_notification_preference", ChannelsModule.Schema);

        builder.HasKey(p => p.Id)
               .HasName("pk_chn005");

        builder.Property(p => p.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(p => p.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(p => p.Category)
               .HasColumnName("category")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(p => p.Channels)
               .HasColumnName("channels")
               .HasColumnType("text[]")
               .IsRequired();

        builder.HasIndex(p => new { p.UserId, p.Category })
               .HasDatabaseName("uq_chn005_user_category")
               .IsUnique();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
    }
}
