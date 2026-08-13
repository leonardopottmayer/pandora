using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Persistence.Storage;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddNotesPersistence(this IServiceCollection services)
    {
        services.AddTarsData<NotesDbContext>(NotesModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<NotesDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));

        // The MVP's only IFileStorage backend: attachment bytes in the notes.nte003_file_blob table.
        services.AddScoped<IFileStorage, DatabaseFileStorage>();

        return services;
    }
}
