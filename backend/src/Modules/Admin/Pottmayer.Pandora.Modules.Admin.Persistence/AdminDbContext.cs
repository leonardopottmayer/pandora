using Microsoft.EntityFrameworkCore;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Admin.Persistence;

internal sealed class AdminDbContext(DbContextOptions<AdminDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
    }
}
