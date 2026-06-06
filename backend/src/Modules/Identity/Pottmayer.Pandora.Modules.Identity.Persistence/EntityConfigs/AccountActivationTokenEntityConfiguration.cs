using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class AccountActivationTokenEntityConfiguration : IEntityTypeConfiguration<AccountActivationToken>
{
    public void Configure(EntityTypeBuilder<AccountActivationToken> builder)
    {
        builder.ToTable("idt004_account_activation_token", IdentityModule.Schema);

        builder.HasKey(t => t.Id)
               .HasName("pk_idt004");

        builder.Property(t => t.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(t => t.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(t => t.TokenHash)
               .HasColumnName("token_hash")
               .HasMaxLength(64)
               .IsRequired();

        builder.HasIndex(t => t.TokenHash)
               .HasDatabaseName("uq_idt004_token_hash")
               .IsUnique();

        builder.Property(t => t.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(t => t.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .HasConstraintName("fk_idt004_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
