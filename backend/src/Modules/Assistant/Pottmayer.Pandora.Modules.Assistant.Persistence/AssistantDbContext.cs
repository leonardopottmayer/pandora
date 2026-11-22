using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence;

internal sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssistantDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join the Assistant's own transaction
        // — the assistant reply is published only if the interpretation it came from committed.
        modelBuilder.AddTarsOutbox(schema: AssistantModule.Schema);
    }
}
