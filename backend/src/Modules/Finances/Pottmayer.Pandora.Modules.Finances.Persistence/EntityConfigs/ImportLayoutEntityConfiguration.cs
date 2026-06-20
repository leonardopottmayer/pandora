using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class ImportLayoutEntityConfiguration : IEntityTypeConfiguration<ImportLayout>
{
    public void Configure(EntityTypeBuilder<ImportLayout> builder)
    {
        builder.ToTable("fin012_import_layout", FinancesModule.Schema);

        builder.HasKey(l => l.Id).HasName("pk_fin012");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.LayoutCode).HasColumnName("layout_code").HasMaxLength(60).IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(l => l.BankName).HasColumnName("bank_name").HasMaxLength(60);
        builder.Property(l => l.FileFormat).HasColumnName("file_format").HasMaxLength(5).IsRequired();
        builder.Property(l => l.AccountType).HasColumnName("account_type").HasMaxLength(10).IsRequired();
        builder.Property(l => l.Config).HasColumnName("config").HasColumnType("jsonb").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
