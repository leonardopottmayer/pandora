using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class DeviceEntityConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("idt009_device", IdentityModule.Schema);

        builder.HasKey(d => d.Id)
               .HasName("pk_idt009");

        builder.Property(d => d.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(d => d.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(d => d.Name)
               .HasColumnName("name")
               .HasMaxLength(Device.NameMaxLength)
               .IsRequired();

        builder.Property(d => d.Platform)
               .HasColumnName("platform")
               .HasConversion(new DevicePlatformConverter())
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(d => d.Form)
               .HasColumnName("form")
               .HasConversion(new DeviceFormConverter())
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(d => d.KeyHash)
               .HasColumnName("key_hash")
               .HasMaxLength(64)
               .IsRequired();

        builder.HasIndex(d => d.KeyHash)
               .HasDatabaseName("uq_idt009_key_hash")
               .IsUnique();

        builder.Property(d => d.Scopes)
               .HasColumnName("scopes")
               .HasColumnType("text[]")
               .IsRequired();

        builder.Property(d => d.CreatedAt)
               .HasColumnName("created_at")
               .IsRequired();

        builder.Property(d => d.LastSeenAt)
               .HasColumnName("last_seen_at");

        builder.Property(d => d.RevokedAt)
               .HasColumnName("revoked_at");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(d => d.UserId)
               .HasConstraintName("fk_idt009_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
