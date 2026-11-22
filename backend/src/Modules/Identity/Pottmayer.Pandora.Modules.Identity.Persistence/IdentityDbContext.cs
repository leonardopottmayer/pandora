using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Tars.Data.Relational;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;

namespace Pottmayer.Pandora.Modules.Identity.Persistence;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // The transactional outbox lives in this context so its rows join Identity's own transaction.
        modelBuilder.AddTarsOutbox(schema: IdentityModule.Schema);
    }
}
