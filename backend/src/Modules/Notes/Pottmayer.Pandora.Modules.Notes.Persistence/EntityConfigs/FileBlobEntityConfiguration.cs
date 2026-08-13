using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Persistence.Storage;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class FileBlobEntityConfiguration : IEntityTypeConfiguration<FileBlob>
{
    public void Configure(EntityTypeBuilder<FileBlob> builder)
    {
        builder.ToTable("nte003_file_blob", NotesModule.Schema);

        builder.HasKey(b => b.Id).HasName("pk_nte003");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(b => b.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(b => b.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(b => b.Content).HasColumnName("content").HasColumnType("bytea").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
