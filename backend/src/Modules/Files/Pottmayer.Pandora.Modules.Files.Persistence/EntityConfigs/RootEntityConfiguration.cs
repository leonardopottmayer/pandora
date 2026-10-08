using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Persistence.EntityConfigs;

internal sealed class RootEntityConfiguration : IEntityTypeConfiguration<Root>
{
    public void Configure(EntityTypeBuilder<Root> builder)
    {
        builder.ToTable("fil001_root", FilesModule.Schema);

        builder.HasKey(r => r.Id).HasName("pk_fil001");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(Root.NameMaxLength).IsRequired();
        builder.Property(r => r.LocalPath).HasColumnName("local_path").HasMaxLength(Root.LocalPathMaxLength).IsRequired();
        builder.Property(r => r.CaseSensitive).HasColumnName("case_sensitive").IsRequired();
        builder.Property(r => r.IncludeHidden).HasColumnName("include_hidden").IsRequired();
        builder.Property(r => r.ScanTime).HasColumnName("scan_time");

        builder.Property(r => r.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => RootStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(r => r.LastCompletedScanAt).HasColumnName("last_completed_scan_at");
        builder.Property(r => r.EntryCount).HasColumnName("entry_count").IsRequired();

        builder.HasMany(r => r.Marks)
               .WithOne()
               .HasForeignKey(m => m.RootId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Marks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
    }
}

internal sealed class SelectionMarkEntityConfiguration : IEntityTypeConfiguration<SelectionMark>
{
    public void Configure(EntityTypeBuilder<SelectionMark> builder)
    {
        builder.ToTable("fil005_selection_mark", FilesModule.Schema);

        builder.HasKey(m => m.Id).HasName("pk_fil005");

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.RootId).HasColumnName("root_id").IsRequired();
        builder.Property(m => m.Path).HasColumnName("path").IsRequired();

        builder.Property(m => m.Mode)
               .HasColumnName("mode")
               .HasConversion(s => s.Value, v => SelectionMode.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        // Known to EF so a replaced set deletes the old marks before inserting the new ones.
        builder.HasIndex(m => new { m.RootId, m.Path }).HasDatabaseName("uq_fil005_root_path").IsUnique();
    }
}
