using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence;

internal sealed class IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationsDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join Integrations' own transaction.
        modelBuilder.AddTarsOutbox(schema: IntegrationsModule.Schema);
    }
}
