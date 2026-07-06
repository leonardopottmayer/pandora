using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class ImportFileEntityConfiguration : IEntityTypeConfiguration<ImportFile>
{
    public void Configure(EntityTypeBuilder<ImportFile> builder)
    {
        builder.ToTable("fin013_import_file", FinancesModule.Schema);

        builder.HasKey(f => f.Id).HasName("pk_fin013");

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.LayoutId).HasColumnName("layout_id");
        builder.Property(f => f.AccountId).HasColumnName("account_id");
        builder.Property(f => f.CardId).HasColumnName("card_id");
        builder.Property(f => f.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(f => f.FileHash).HasColumnName("file_hash").HasMaxLength(64).IsRequired();
        builder.Property(f => f.FileContent).HasColumnName("file_content").HasColumnType("bytea").IsRequired();
        builder.Property(f => f.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(f => f.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(f => f.CutoffDate).HasColumnName("cutoff_date");
        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion(s => s.Value, v => ImportFileStatus.FromValue(v))
            .HasMaxLength(15)
            .IsRequired();
        builder.Property(f => f.TotalRows).HasColumnName("total_rows").IsRequired();
        builder.Property(f => f.ParsedRows).HasColumnName("parsed_rows").IsRequired();
        builder.Property(f => f.ErrorRows).HasColumnName("error_rows").IsRequired();
        builder.Property(f => f.DuplicateRows).HasColumnName("duplicate_rows").IsRequired();
        builder.Property(f => f.SuggestionRows).HasColumnName("suggestion_rows").IsRequired();
        builder.Property(f => f.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(f => f.ErrorMessage).HasColumnName("error_message");
        builder.Property(f => f.StartedAt).HasColumnName("started_at");
        builder.Property(f => f.CompletedAt).HasColumnName("completed_at");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<ImportLayout>()
            .WithMany()
            .HasForeignKey(f => f.LayoutId)
            .HasConstraintName("fk_fin013_layout_id");

        builder.HasIndex(f => new { f.UserId, f.Status }).HasDatabaseName("ix_fin013_user_status");
        builder.HasIndex(f => f.CorrelationId).HasDatabaseName("ix_fin013_correlation_id");
    }
}
