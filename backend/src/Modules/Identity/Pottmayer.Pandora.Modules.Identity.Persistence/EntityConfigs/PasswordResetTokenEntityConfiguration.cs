using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class PasswordResetTokenEntityConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("idt005_password_reset_token", IdentityModule.Schema);

        builder.HasKey(t => t.Id)
               .HasName("pk_idt005");

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
               .HasDatabaseName("uq_idt005_token_hash")
               .IsUnique();

        builder.Property(t => t.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(t => t.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .HasConstraintName("fk_idt005_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
