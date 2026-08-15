using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class TagEntityConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("nte005_tag", NotesModule.Schema);

        builder.HasKey(t => t.Id).HasName("pk_nte005");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(TagName.MaxLength).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(TagName.MaxLength).IsRequired();
        builder.Property(t => t.Color).HasColumnName("color").HasMaxLength(20);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        // The slug is the tag's identity within the user: two spellings of the same word are one tag.
        builder.HasIndex(t => new { t.UserId, t.Slug })
               .HasDatabaseName("uq_nte005_user_slug")
               .IsUnique();
    }
}
