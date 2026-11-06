using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.EntityConfigs;

internal sealed class UserPermissionEntityConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("adm005_user_permission", AdminModule.Schema);

        builder.HasKey(p => new { p.UserId, p.Pattern }).HasName("pk_adm005");

        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.Pattern).HasColumnName("pattern").HasMaxLength(150);
        builder.Property(p => p.Effect).HasColumnName("effect").HasMaxLength(10).IsRequired();
    }
}
