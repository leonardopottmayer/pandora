using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.EntityConfigs;

internal sealed class ExternalAccountEntityConfiguration : IEntityTypeConfiguration<ExternalAccount>
{
    public void Configure(EntityTypeBuilder<ExternalAccount> builder)
    {
        builder.ToTable("int001_external_account", IntegrationsModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_int001");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(a => a.Provider)
               .HasColumnName("provider")
               .HasMaxLength(40)
               .IsRequired();

        builder.Property(a => a.AuthKind)
               .HasColumnName("auth_kind")
               .HasConversion(k => k.Value, v => AuthKind.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(a => a.ProviderAccountId)
               .HasColumnName("provider_account_id")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(a => a.DisplayName)
               .HasColumnName("display_name")
               .HasMaxLength(255);

        builder.Property(a => a.Scopes)
               .HasColumnName("scopes")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(a => a.AccessTokenEnc).HasColumnName("access_token_enc").HasColumnType("text");
        builder.Property(a => a.AccessTokenExpiresAt).HasColumnName("access_token_expires_at");
        builder.Property(a => a.RefreshTokenEnc).HasColumnName("refresh_token_enc").HasColumnType("text");

        builder.Property(a => a.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => AccountStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(a => a.ConnectedAt).HasColumnName("connected_at").IsRequired();
        builder.Property(a => a.LastRefreshedAt).HasColumnName("last_refreshed_at");
        builder.Property(a => a.LastError).HasColumnName("last_error").HasColumnType("text");

        builder.HasIndex(a => new { a.UserId, a.Provider, a.ProviderAccountId })
               .HasDatabaseName("uq_int001_user_provider_account")
               .IsUnique();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
    }
}
