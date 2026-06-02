using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Users.Domain.Entities;
using Pottmayer.Pandora.Modules.Users.Persistence.ValueConverters;

namespace Pottmayer.Pandora.Modules.Users.Persistence.EntityConfigs;

internal sealed class UserPreferencesEntityConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("usr002_user_preferences", UsersModule.Schema, tb =>
        {
            tb.HasCheckConstraint(
                "chk_usr002_user_preferences_theme",
                "theme IN ('light', 'dark', 'system')");
        });

        builder.HasKey(p => p.Id)
               .HasName("pk_usr002_user_preferences");

        builder.Property(p => p.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(p => p.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.HasIndex(p => p.UserId)
               .HasDatabaseName("uq_usr002_user_preferences_user_id")
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
               .HasConstraintName("fk_usr002_user_preferences_created_by")
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(p => p.UpdatedBy)
               .HasPrincipalKey(u => u.Id)
               .HasConstraintName("fk_usr002_user_preferences_updated_by")
               .OnDelete(DeleteBehavior.Restrict);
    }
}
