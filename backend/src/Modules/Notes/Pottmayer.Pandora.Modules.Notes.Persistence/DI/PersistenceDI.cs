using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Persistence.Interceptors;
using Pottmayer.Pandora.Shared.Persistence.DI;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.DI;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddNotesPersistence(this IServiceCollection services)
    {
        services.AddScoped<PageVersionSaveChangesInterceptor>();

        services.AddTarsRelationalData<NotesDbContext>(NotesModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<NotesDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .AddInterceptors(sp.GetRequiredService<PageVersionSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));

        // Attachment bytes in the notes.nte003_file_blob table, keyed by this module's database key.
        services.AddPandoraDatabaseFileStorage(NotesModule.DatabaseKey);

        return services;
    }
}
