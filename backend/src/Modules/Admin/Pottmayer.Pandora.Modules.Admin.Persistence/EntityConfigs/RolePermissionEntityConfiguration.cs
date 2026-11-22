using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.EntityConfigs;

internal sealed class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("adm003_role_permission", AdminModule.Schema);

        builder.HasKey(p => new { p.RoleId, p.Pattern }).HasName("pk_adm003");

        builder.Property(p => p.RoleId).HasColumnName("role_id");
        builder.Property(p => p.Pattern).HasColumnName("pattern").HasMaxLength(150);
    }
}
