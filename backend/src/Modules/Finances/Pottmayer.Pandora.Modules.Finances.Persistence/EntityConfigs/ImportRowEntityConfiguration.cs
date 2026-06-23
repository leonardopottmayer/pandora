using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class ImportRowEntityConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("fin014_import_row", FinancesModule.Schema);

        builder.HasKey(r => r.Id).HasName("pk_fin014");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.ImportFileId).HasColumnName("import_file_id").IsRequired();
        builder.Property(r => r.RowIndex).HasColumnName("row_index").IsRequired();
        builder.Property(r => r.RawData).HasColumnName("raw_data").IsRequired();
        builder.Property(r => r.ParsedPayload).HasColumnName("parsed_payload").HasColumnType("jsonb");
        builder.Property(r => r.ExternalId).HasColumnName("external_id").HasMaxLength(255);
        builder.Property(r => r.DedupKey).HasColumnName("dedup_key").HasMaxLength(64);
        builder.Property(r => r.DedupStatus)
            .HasColumnName("dedup_status")
            .HasConversion(d => d.Value, v => DedupStatus.FromValue(v))
            .HasMaxLength(15)
            .IsRequired();
        builder.Property(r => r.MatchedTransactionId).HasColumnName("matched_transaction_id");
        builder.Property(r => r.MatchedPendingTransactionId).HasColumnName("matched_pending_transaction_id");
        builder.Property(r => r.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(r => r.InstallmentCount).HasColumnName("installment_count");
        builder.Property(r => r.MatchedInstallmentPlanId).HasColumnName("matched_installment_plan_id");
        builder.Property(r => r.PendingTransactionId).HasColumnName("pending_transaction_id");
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(s => s.Value, v => ImportRowStatus.FromValue(v))
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<ImportFile>()
            .WithMany()
            .HasForeignKey(r => r.ImportFileId)
            .HasConstraintName("fk_fin014_import_file_id");

        builder.HasIndex(r => r.ImportFileId).HasDatabaseName("ix_fin014_import_file_id");
        builder.HasIndex(r => r.DedupKey)
            .HasFilter("dedup_key IS NOT NULL")
            .HasDatabaseName("ix_fin014_dedup_key");
        builder.HasIndex(r => r.ExternalId)
            .HasFilter("external_id IS NOT NULL")
            .HasDatabaseName("ix_fin014_external_id");
    }
}
