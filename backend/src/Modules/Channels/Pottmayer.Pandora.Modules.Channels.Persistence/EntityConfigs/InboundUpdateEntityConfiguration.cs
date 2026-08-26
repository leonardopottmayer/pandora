using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.EntityConfigs;

internal sealed class InboundUpdateEntityConfiguration : IEntityTypeConfiguration<InboundUpdate>
{
    public void Configure(EntityTypeBuilder<InboundUpdate> builder)
    {
        builder.ToTable("chn004_inbound_update", ChannelsModule.Schema);

        builder.HasKey(u => u.Id)
               .HasName("pk_chn004");

        builder.Property(u => u.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(u => u.Provider)
               .HasColumnName("provider")
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(u => u.ProviderUpdateId)
               .HasColumnName("provider_update_id")
               .IsRequired();

        // Nullable: the retention job clears it to null once the payload ages out (see InboundUpdate.Raw).
        builder.Property(u => u.Raw)
               .HasColumnName("raw")
               .HasColumnType("jsonb");

        builder.Property(u => u.UserId)
               .HasColumnName("user_id");

        builder.Property(u => u.Classification)
               .HasColumnName("classification")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(u => u.ReceivedAt)
               .HasColumnName("received_at")
               .IsRequired();

        builder.Property(u => u.ProcessedAt)
               .HasColumnName("processed_at");

        builder.HasIndex(u => new { u.Provider, u.ProviderUpdateId })
               .HasDatabaseName("uq_chn004_provider_update")
               .IsUnique();
    }
}
