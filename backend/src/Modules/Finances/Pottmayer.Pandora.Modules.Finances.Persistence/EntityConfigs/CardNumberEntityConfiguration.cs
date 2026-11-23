using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class CardNumberEntityConfiguration : IEntityTypeConfiguration<CardNumber>
{
    public void Configure(EntityTypeBuilder<CardNumber> builder)
    {
        builder.ToTable("fin019_card_number", FinancesModule.Schema);

        builder.HasKey(n => n.Id).HasName("pk_fin019");

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(n => n.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(n => n.Kind)
            .HasColumnName("kind")
            .HasConversion(k => k.Value, v => CardNumberKind.FromValue(v))
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(n => n.LastFour).HasColumnName("last_four").HasMaxLength(4).IsRequired();
        builder.Property(n => n.Nickname).HasColumnName("nickname").HasMaxLength(50);
        builder.Property(n => n.ArchivedAt).HasColumnName("archived_at");

        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.CreatedBy).HasColumnName("created_by");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(n => n.CardId)
            .HasConstraintName("fk_fin019_card_id");

        builder.HasIndex(n => new { n.CardId, n.LastFour }).IsUnique().HasDatabaseName("uq_fin019_card_last_four");
    }
}
