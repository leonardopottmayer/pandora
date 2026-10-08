using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Files.Persistence.EntityConfigs;

internal sealed class PreferencesEntityConfiguration : IEntityTypeConfiguration<Preferences>
{
    public void Configure(EntityTypeBuilder<Preferences> builder)
    {
        builder.ToTable("fil004_preferences", FilesModule.Schema);

        builder.HasKey(p => p.UserId).HasName("pk_fil004");

        builder.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(p => p.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
    }
}
