using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class AccountEntityConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("fin001_account", FinancesModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_fin001");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.Property(a => a.Type)
               .HasColumnName("type")
               .HasConversion(t => t.Value, v => AccountType.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(a => a.Currency)
               .HasColumnName("currency")
               .HasConversion(c => c.Value, v => CurrencyCode.Create(v))
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(a => a.Institution).HasColumnName("institution").HasMaxLength(100);
        builder.Property(a => a.Description).HasColumnName("description").HasMaxLength(255);
        builder.Property(a => a.Color).HasColumnName("color").HasMaxLength(20);
        builder.Property(a => a.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(a => a.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(a => a.ArchivedAt).HasColumnName("archived_at");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(a => new { a.UserId, a.Name })
               .HasDatabaseName("uq_fin001_user_name")
               .IsUnique();
    }
}
