using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.EntityConfigs;

internal sealed class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("adm004_user_role", AdminModule.Schema);

        builder.HasKey(r => new { r.UserId, r.RoleId }).HasName("pk_adm004");

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.RoleId).HasColumnName("role_id");
    }
}
