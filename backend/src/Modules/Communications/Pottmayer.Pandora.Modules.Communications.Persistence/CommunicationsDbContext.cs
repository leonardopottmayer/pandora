using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Communications.Persistence;

internal sealed class CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationsDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join Communications' own transaction.
        modelBuilder.AddTarsOutbox(schema: CommunicationsModule.Schema);
    }
}
