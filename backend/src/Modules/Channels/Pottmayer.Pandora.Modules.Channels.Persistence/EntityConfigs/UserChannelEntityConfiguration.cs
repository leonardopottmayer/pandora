using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class UserChannelEntityConfiguration : IEntityTypeConfiguration<UserChannel>
{
    public void Configure(EntityTypeBuilder<UserChannel> builder)
    {
        builder.ToTable("chn001_user_channel", ChannelsModule.Schema);

        builder.HasKey(c => c.Id)
               .HasName("pk_chn001");

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(c => c.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(c => c.Channel)
               .HasColumnName("channel")
               .HasConversion(c => c.Value, v => Channel.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(c => c.Address)
               .HasColumnName("address")
               .HasConversion(a => a.Value, v => NotificationAddress.FromValue(v))
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(c => c.Locale)
               .HasColumnName("locale")
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(c => c.IsVerified)
               .HasColumnName("is_verified")
               .IsRequired();

        builder.Property(c => c.VerifiedAt)
               .HasColumnName("verified_at");

        builder.Property(c => c.IsEnabled)
               .HasColumnName("is_enabled")
               .IsRequired();

        builder.Property(c => c.DisabledReason)
               .HasColumnName("disabled_reason")
               .HasColumnType("text");

        builder.Property(c => c.Metadata)
               .HasColumnName("metadata")
               .HasColumnType("jsonb")
               .IsRequired();

        builder.HasIndex(c => new { c.UserId, c.Channel })
               .HasDatabaseName("uq_chn001_user_channel")
               .IsUnique();

        builder.HasIndex(c => new { c.Channel, c.Address })
               .HasDatabaseName("uq_chn001_channel_address")
               .IsUnique();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
    }
}
