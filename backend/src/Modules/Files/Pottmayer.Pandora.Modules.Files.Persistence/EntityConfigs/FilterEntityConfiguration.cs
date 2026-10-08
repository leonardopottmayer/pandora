using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Persistence.EntityConfigs;

internal sealed class FilterEntityConfiguration : IEntityTypeConfiguration<Filter>
{
    public void Configure(EntityTypeBuilder<Filter> builder)
    {
        builder.ToTable("fil006_filter", FilesModule.Schema);

        builder.HasKey(f => f.Id).HasName("pk_fil006");

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.DeviceId).HasColumnName("device_id");
        builder.Property(f => f.RootId).HasColumnName("root_id");
        builder.Property(f => f.ScopePath).HasColumnName("scope_path");
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(Filter.NameMaxLength).IsRequired();

        builder.Property(f => f.Action)
               .HasColumnName("action")
               .HasConversion(a => a.Value, v => FilterAction.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(f => f.AppliesTo)
               .HasColumnName("applies_to")
               .HasConversion(t => t.Value, v => FilterTarget.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(f => f.Matcher)
               .HasColumnName("matcher")
               .HasConversion(m => m.Value, v => FilterMatcher.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(f => f.Pattern).HasColumnName("pattern").HasMaxLength(Filter.PatternMaxLength).IsRequired();
        builder.Property(f => f.CaseSensitive).HasColumnName("case_sensitive");
        builder.Property(f => f.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(f => f.IsBuiltin).HasColumnName("is_builtin").IsRequired();

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");
    }
}
