using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Storage;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Notes.Persistence;

internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : RelationalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotesDbContext).Assembly);
        modelBuilder.MapFileBlobs(NotesModule.Schema, "nte003_file_blob", "pk_nte003");
    }
}
