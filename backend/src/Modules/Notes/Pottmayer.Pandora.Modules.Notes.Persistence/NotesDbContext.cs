using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Shared.Persistence.Storage;
using Pottmayer.Tars.Data.Relational;

namespace Pottmayer.Pandora.Modules.Notes.Persistence;

internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : RelationalDbContext(options)
{
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PageHistoryConflictException();
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_nte011_page_number" })
        {
            throw new PageHistoryConflictException();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotesDbContext).Assembly);
        modelBuilder.MapFileBlobs(NotesModule.Schema, "nte003_file_blob", "pk_nte003");
    }
}
