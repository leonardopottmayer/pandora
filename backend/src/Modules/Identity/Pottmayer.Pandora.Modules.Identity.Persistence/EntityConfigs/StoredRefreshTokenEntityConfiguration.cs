using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class StoredRefreshTokenEntityConfiguration : IEntityTypeConfiguration<StoredRefreshToken>
{
    public void Configure(EntityTypeBuilder<StoredRefreshToken> builder)
    {
        builder.ToTable("idt001_stored_refresh_token", IdentityModule.Schema);

        builder.HasKey(t => t.Id)
               .HasName("pk_idt001_stored_refresh_token");

        builder.Property(t => t.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(t => t.TokenId)
               .HasColumnName("key")
               .HasMaxLength(100)
               .IsRequired();

        builder.HasIndex(t => t.TokenId)
               .HasDatabaseName("uq_idt001_stored_refresh_token_key")
               .IsUnique();

        builder.Property(t => t.TokenHash)
               .HasColumnName("token_hash")
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(t => t.Subject)
               .HasColumnName("subject")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(t => t.ClaimsJson)
               .HasColumnName("claims_json")
               .IsRequired();

        builder.Property(t => t.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(t => t.MetadataJson)
               .HasColumnName("metadata_json");

        builder.Property(t => t.ConsumedAt)
               .HasColumnName("consumed_at");
    }
}
