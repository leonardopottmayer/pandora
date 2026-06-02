using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Users.Domain.Entities;
using Pottmayer.Pandora.Modules.Users.Persistence.ValueConverters;
using Pottmayer.Pandora.Shared.Persistence.ValueConverters;

namespace Pottmayer.Pandora.Modules.Users.Persistence.EntityConfigs;

internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("usr001_user", UsersModule.Schema, tb =>
        {
            tb.HasCheckConstraint(
                "chk_usr001_user_status",
                "status IN ('active', 'blocked')");
        });

        builder.HasKey(u => u.Id)
               .HasName("pk_usr001_user");

        builder.Property(u => u.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.HasIndex(u => u.Email)
               .HasDatabaseName("uq_usr001_user_email")
               .IsUnique();
        builder.HasIndex(u => u.Username)
               .HasDatabaseName("uq_usr001_user_username")
               .IsUnique();

        builder.Property(u => u.Name)
               .HasColumnName("name")
               .HasMaxLength(150)
               .IsRequired();

        builder.Property(u => u.Username)
               .HasColumnName("username")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(u => u.Password)
               .HasColumnName("password")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(u => u.Status)
               .HasColumnName("status")
               .HasConversion(new UserStatusConverter())
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(u => u.Email)
               .HasColumnName("email")
               .HasConversion(new EmailValueConverter())
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(u => u.Preferences)
               .WithOne()
               .HasForeignKey<UserPreferences>(p => p.UserId)
               .HasConstraintName("fk_usr002_user_preferences_user_id")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(u => u.CreatedBy)
               .HasPrincipalKey(u => u.Id)
               .HasConstraintName("fk_usr001_user_created_by")
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(u => u.UpdatedBy)
               .HasPrincipalKey(u => u.Id)
               .HasConstraintName("fk_usr001_user_updated_by")
               .OnDelete(DeleteBehavior.Restrict);
    }
}
