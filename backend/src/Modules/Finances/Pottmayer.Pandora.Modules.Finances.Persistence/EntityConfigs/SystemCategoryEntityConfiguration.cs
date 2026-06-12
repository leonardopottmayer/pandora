using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class SystemCategoryEntityConfiguration : IEntityTypeConfiguration<SystemCategory>
{
    public void Configure(EntityTypeBuilder<SystemCategory> builder)
    {
        builder.ToTable("fin002_system_category", FinancesModule.Schema);

        builder.HasKey(c => c.Id).HasName("pk_fin002");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(60).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.Property(c => c.Nature)
               .HasColumnName("transaction_nature")
               .HasConversion(n => n.Value, v => TransactionNature.FromValue(v))
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id");
        builder.Property(c => c.Color).HasColumnName("color").HasMaxLength(20);
        builder.Property(c => c.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(c => c.IsOther).HasColumnName("is_other").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(255);
    }
}
