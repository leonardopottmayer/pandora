using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.System.Abstractions;
using Pottmayer.Pandora.Modules.System.Domain.Entities;

namespace Pottmayer.Pandora.Modules.System.Persistence.EntityConfigs;

internal sealed class DomainItemEntityConfiguration : IEntityTypeConfiguration<DomainItem>
{
    public void Configure(EntityTypeBuilder<DomainItem> builder)
    {
        builder.ToTable("sys001_domain", SystemModule.Schema);

        builder.HasKey(d => d.Id)
               .HasName("pk_sys001_domain");

        builder.Property(d => d.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("uuid_generate_v7()")
               .ValueGeneratedOnAdd();

        builder.HasIndex(d => new { d.DomainName, d.ItemValue })
               .HasDatabaseName("uq_sys001_domain_domain_name_item_value")
               .IsUnique();

        builder.Property(d => d.DomainName)
               .HasColumnName("domain_name")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(d => d.ItemName)
               .HasColumnName("item_name")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(d => d.ItemValue)
               .HasColumnName("item_value")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(d => d.ItemDescription)
               .HasColumnName("item_description");
    }
}
