using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class MfaCredentialEntityConfiguration : IEntityTypeConfiguration<MfaCredential>
{
    public void Configure(EntityTypeBuilder<MfaCredential> builder)
    {
        builder.ToTable("idt006_mfa_credential", IdentityModule.Schema);

        builder.HasKey(c => c.Id)
               .HasName("pk_idt006");

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(c => c.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.HasIndex(c => c.UserId)
               .HasDatabaseName("uq_idt006_user_id")
               .IsUnique();

        builder.Property(c => c.SecretCipher)
               .HasColumnName("secret_cipher")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(c => c.ConfirmedAt)
               .HasColumnName("confirmed_at");

        builder.Property(c => c.CreatedAt)
               .HasColumnName("created_at")
               .IsRequired();

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .HasConstraintName("fk_idt006_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
