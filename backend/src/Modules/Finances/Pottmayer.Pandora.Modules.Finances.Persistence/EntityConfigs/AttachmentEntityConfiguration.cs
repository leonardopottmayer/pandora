using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class AttachmentEntityConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("fin017_attachment", FinancesModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_fin017");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.TransactionId).HasColumnName("transaction_id");
        builder.Property(a => a.PendingTransactionId).HasColumnName("pending_transaction_id");
        builder.Property(a => a.Kind)
               .HasColumnName("kind")
               .HasMaxLength(20)
               .HasConversion(k => k.Value, v => AttachmentKind.FromValue(v))
               .IsRequired();
        builder.Property(a => a.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(a => a.StorageBackend).HasColumnName("storage_backend").HasMaxLength(50).IsRequired();
        builder.Property(a => a.StorageKey).HasColumnName("storage_key").HasMaxLength(1024).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(a => a.TransactionId).HasDatabaseName("ix_fin017_transaction_id");
        builder.HasIndex(a => a.PendingTransactionId).HasDatabaseName("ix_fin017_pending_transaction_id");
    }
}
