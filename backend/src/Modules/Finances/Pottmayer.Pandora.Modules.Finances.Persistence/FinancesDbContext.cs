using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Storage;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Finances.Persistence;

internal sealed class FinancesDbContext(DbContextOptions<FinancesDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancesDbContext).Assembly);
        modelBuilder.MapFileBlobs(FinancesModule.Schema, "fin018_file_blob", "pk_fin018");
    }
}
