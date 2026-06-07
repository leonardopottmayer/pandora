using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class MfaChallengeEntityConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("idt008_mfa_challenge", IdentityModule.Schema);

        builder.HasKey(c => c.Id)
               .HasName("pk_idt008");

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(c => c.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.HasIndex(c => c.UserId)
               .HasDatabaseName("ix_idt008_user_id");

        builder.Property(c => c.TokenHash)
               .HasColumnName("token_hash")
               .HasMaxLength(64)
               .IsRequired();

        builder.HasIndex(c => c.TokenHash)
               .HasDatabaseName("uq_idt008_token_hash")
               .IsUnique();

        builder.Property(c => c.ExpiresAt)
               .HasColumnName("expires_at")
               .IsRequired();

        builder.Property(c => c.ConsumedAt)
               .HasColumnName("consumed_at");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .HasConstraintName("fk_idt008_user_id")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
