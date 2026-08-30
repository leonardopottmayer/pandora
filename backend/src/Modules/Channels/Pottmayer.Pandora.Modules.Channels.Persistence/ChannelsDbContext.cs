using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Channels.Persistence;

internal sealed class ChannelsDbContext(DbContextOptions<ChannelsDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChannelsDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join Channels' own transaction.
        modelBuilder.AddTarsOutbox(schema: ChannelsModule.Schema);
    }
}
