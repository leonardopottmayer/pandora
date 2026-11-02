using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class PageVersionEntityConfiguration : IEntityTypeConfiguration<PageVersion>
{
    public void Configure(EntityTypeBuilder<PageVersion> builder)
    {
        builder.ToTable("nte011_page_version", NotesModule.Schema);

        builder.HasKey(v => v.Id).HasName("pk_nte011");

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(v => v.Number).HasColumnName("number").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
        builder.Property(v => v.RestoredFrom).HasColumnName("restored_from");
        builder.Property(v => v.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb").IsRequired()
            .HasConversion(s => JsonSerializer.Serialize(s, (JsonSerializerOptions?)null),
                s => JsonSerializer.Deserialize<PageSnapshot>(s, (JsonSerializerOptions?)null)!);

        builder.HasIndex(v => new { v.PageId, v.Number }).IsUnique().HasDatabaseName("uq_nte011_page_number");
        builder.HasOne<Page>().WithMany().HasForeignKey(v => v.PageId).HasConstraintName("fk_nte011_page");
    }
}
