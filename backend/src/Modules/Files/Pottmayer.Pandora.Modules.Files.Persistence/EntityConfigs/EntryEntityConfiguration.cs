using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Persistence.EntityConfigs;

internal sealed class EntryEntityConfiguration : IEntityTypeConfiguration<Entry>
{
    public void Configure(EntityTypeBuilder<Entry> builder)
    {
        builder.ToTable("fil002_entry", FilesModule.Schema);

        builder.HasKey(e => e.Id).HasName("pk_fil002");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.RootId).HasColumnName("root_id").IsRequired();

        builder.Property(e => e.Kind)
               .HasColumnName("kind")
               .HasConversion(k => k.Value, v => EntryKind.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(e => e.RelativePath).HasColumnName("relative_path").IsRequired();
        builder.Property(e => e.ParentPath).HasColumnName("parent_path").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.Extension).HasColumnName("extension").HasMaxLength(50);

        builder.Property(e => e.Category)
               .HasColumnName("category")
               .HasConversion(c => c!.Value, v => FileCategory.FromValue(v))
               .HasMaxLength(20);

        builder.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);

        builder.Property(e => e.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => EntryStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(e => e.MissingSince).HasColumnName("missing_since");
        builder.Property(e => e.KeptAt).HasColumnName("kept_at");
        builder.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at").IsRequired();
        builder.Property(e => e.LastSeenScanId).HasColumnName("last_seen_scan_id");
    }
}
