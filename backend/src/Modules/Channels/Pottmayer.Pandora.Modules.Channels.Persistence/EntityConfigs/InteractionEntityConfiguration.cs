using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class InteractionEntityConfiguration : IEntityTypeConfiguration<Interaction>
{
    public void Configure(EntityTypeBuilder<Interaction> builder)
    {
        builder.ToTable("chn003_interaction", ChannelsModule.Schema);

        builder.HasKey(i => i.Id)
               .HasName("pk_chn003");

        builder.Property(i => i.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(i => i.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(i => i.OwnerModule)
               .HasColumnName("owner_module")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(i => i.Action)
               .HasColumnName("action")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(i => i.Payload)
               .HasColumnName("payload")
               .HasColumnType("text");

        builder.Property(i => i.NotificationId)
               .HasColumnName("notification_id");

        // Declares the FK so EF orders the notification insert before its buttons, and matches the
        // migration's ON DELETE SET NULL.
        builder.HasOne<Notification>()
               .WithMany()
               .HasForeignKey(i => i.NotificationId)
               .HasConstraintName("fk_chn003_notification")
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(i => i.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(i => i.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
    }
}
