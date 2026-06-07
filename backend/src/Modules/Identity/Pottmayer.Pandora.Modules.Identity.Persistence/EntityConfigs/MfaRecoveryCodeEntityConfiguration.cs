using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class MfaRecoveryCodeEntityConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("idt007_mfa_recovery_code", IdentityModule.Schema);

        builder.HasKey(c => c.Id)
               .HasName("pk_idt007");

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(c => c.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.HasIndex(c => c.UserId)
               .HasDatabaseName("ix_idt007_user_id");

        builder.Property(c => c.CodeHash)
               .HasColumnName("code_hash")
               .HasMaxLength(64)
               .IsRequired();

        builder.HasIndex(c => c.CodeHash)
               .HasDatabaseName("uq_idt007_code_hash")
               .IsUnique();

        builder.Property(c => c.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.Property(c => c.CreatedAt)
               .HasColumnName("created_at")
               .IsRequired();

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .HasConstraintName("fk_idt007_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
