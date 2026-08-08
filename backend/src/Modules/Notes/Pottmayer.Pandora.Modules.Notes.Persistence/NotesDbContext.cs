using Microsoft.EntityFrameworkCore;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Notes.Persistence;

internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotesDbContext).Assembly);
    }
}
