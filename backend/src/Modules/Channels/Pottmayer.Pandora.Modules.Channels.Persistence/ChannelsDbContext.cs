using Microsoft.EntityFrameworkCore;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Channels.Persistence;

internal sealed class ChannelsDbContext(DbContextOptions<ChannelsDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChannelsDbContext).Assembly);
    }
}
