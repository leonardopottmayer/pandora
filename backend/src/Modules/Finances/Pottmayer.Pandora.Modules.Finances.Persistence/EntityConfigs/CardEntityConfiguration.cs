using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class CardEntityConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("fin006_card", FinancesModule.Schema);

        builder.HasKey(c => c.Id).HasName("pk_fin006");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Brand).HasColumnName("brand").HasMaxLength(50);
        builder.Property(c => c.LastFour).HasColumnName("last_four").HasMaxLength(4);
        builder.Property(c => c.CreditLimit).HasColumnName("credit_limit").HasColumnType("numeric(20,8)");
        builder.Property(c => c.ClosingDay).HasColumnName("closing_day").IsRequired();
        builder.Property(c => c.DueDay).HasColumnName("due_day").IsRequired();
        builder.Property(c => c.Currency)
            .HasColumnName("currency")
            .HasConversion(c => c.Value, v => CurrencyCode.Create(v))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(c => c.DefaultPaymentAccountId).HasColumnName("default_payment_account_id");
        builder.Property(c => c.ArchivedAt).HasColumnName("archived_at");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(c => new { c.UserId, c.ArchivedAt }).HasDatabaseName("ix_fin006_user_archived_at");
    }
}
