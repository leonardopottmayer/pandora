using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.EntityConfigs;

internal sealed class UserPreferencesEntityConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("idt003_user_preferences", IdentityModule.Schema, tb =>
        {
            tb.HasCheckConstraint(
                "chk_idt003_theme",
                "theme IN ('light', 'dark', 'system')");
        });

        builder.HasKey(p => p.Id)
               .HasName("pk_idt003");

        builder.Property(p => p.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(p => p.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.HasIndex(p => p.UserId)
               .HasDatabaseName("uq_idt003_user_id")
               .IsUnique();

        builder.Property(p => p.Theme)
               .HasColumnName("theme")
               .HasConversion(new AppThemeConverter())
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(p => p.CreatedBy)
               .HasPrincipalKey(u => u.Id)
               .HasConstraintName("fk_idt003_created_by")
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(p => p.UpdatedBy)
               .HasPrincipalKey(u => u.Id)
               .HasConstraintName("fk_idt003_updated_by")
               .OnDelete(DeleteBehavior.Restrict);
    }
}
