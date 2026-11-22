using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence;

internal sealed class AgendaDbContext(DbContextOptions<AgendaDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgendaDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join Agenda's own transaction.
        modelBuilder.AddTarsOutbox(schema: AgendaModule.Schema);
    }
}
