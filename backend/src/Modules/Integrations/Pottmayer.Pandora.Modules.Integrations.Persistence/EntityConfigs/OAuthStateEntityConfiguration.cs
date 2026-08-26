using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.EntityConfigs;

internal sealed class OAuthStateEntityConfiguration : IEntityTypeConfiguration<OAuthState>
{
    public void Configure(EntityTypeBuilder<OAuthState> builder)
    {
        builder.ToTable("int002_oauth_state", IntegrationsModule.Schema);

        builder.HasKey(s => s.Id).HasName("pk_int002");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(s => s.Provider)
               .HasColumnName("provider")
               .HasMaxLength(40)
               .IsRequired();

        builder.Property(s => s.State)
               .HasColumnName("state")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(s => s.CodeVerifierEnc)
               .HasColumnName("code_verifier_enc")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(s => s.RedirectAfter)
               .HasColumnName("redirect_after")
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.ConsumedAt).HasColumnName("consumed_at");

        builder.HasIndex(s => s.State)
               .HasDatabaseName("uq_int002_state")
               .IsUnique();
    }
}
