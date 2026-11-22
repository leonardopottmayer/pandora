using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Persistence.EntityConfigs;

internal sealed class ScanEntityConfiguration : IEntityTypeConfiguration<Scan>
{
    public void Configure(EntityTypeBuilder<Scan> builder)
    {
        builder.ToTable("fil003_scan", FilesModule.Schema);

        builder.HasKey(s => s.Id).HasName("pk_fil003");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.RootId).HasColumnName("root_id").IsRequired();

        builder.Property(s => s.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => ScanStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(s => s.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(s => s.LastBatchAt).HasColumnName("last_batch_at");
        builder.Property(s => s.FinishedAt).HasColumnName("finished_at");
        builder.Property(s => s.Seen).HasColumnName("seen").IsRequired();
        builder.Property(s => s.Created).HasColumnName("created").IsRequired();
        builder.Property(s => s.Changed).HasColumnName("changed").IsRequired();
        builder.Property(s => s.Moved).HasColumnName("moved").IsRequired();
        builder.Property(s => s.Missing).HasColumnName("missing").IsRequired();
        builder.Property(s => s.Excluded).HasColumnName("excluded").IsRequired();
        builder.Property(s => s.Error).HasColumnName("error").HasMaxLength(Scan.ErrorMaxLength);
    }
}
