using Microsoft.EntityFrameworkCore;

namespace Pottmayer.Pandora.Shared.Persistence.Storage;

public static class FileBlobMapping
{
    /// <summary>
    /// Maps the module's blob table (the bytes behind its <see cref="Domain.Storage.IFileStorage"/>). Call
    /// it from the module's <c>OnModelCreating</c>, next to <see cref="DI.FileStorageDI.AddPandoraDatabaseFileStorage"/>
    /// in its persistence DI.
    /// </summary>
    public static ModelBuilder MapFileBlobs(this ModelBuilder modelBuilder, string schema, string table, string primaryKeyName)
    {
        modelBuilder.Entity<FileBlob>(builder =>
        {
            builder.ToTable(table, schema);

            builder.HasKey(b => b.Id).HasName(primaryKeyName);

            builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(b => b.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            builder.Property(b => b.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
            builder.Property(b => b.SizeBytes).HasColumnName("size_bytes").IsRequired();
            builder.Property(b => b.Content).HasColumnName("content").HasColumnType("bytea").IsRequired();
            builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        });
        return modelBuilder;
    }
}
