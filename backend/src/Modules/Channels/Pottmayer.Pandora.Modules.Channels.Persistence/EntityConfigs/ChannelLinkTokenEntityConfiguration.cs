using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class ChannelLinkTokenEntityConfiguration : IEntityTypeConfiguration<ChannelLinkToken>
{
    public void Configure(EntityTypeBuilder<ChannelLinkToken> builder)
    {
        builder.ToTable("chn002_channel_link_token", ChannelsModule.Schema);

        builder.HasKey(t => t.Id)
               .HasName("pk_chn002");

        builder.Property(t => t.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(t => t.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(t => t.Channel)
               .HasColumnName("channel")
               .HasConversion(c => c.Value, v => Channel.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(t => t.TokenHash)
               .HasColumnName("token")
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(t => t.Locale)
               .HasColumnName("locale")
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(t => t.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(t => t.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.HasIndex(t => t.TokenHash)
               .HasDatabaseName("uq_chn002_token")
               .IsUnique();

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
    }
}
