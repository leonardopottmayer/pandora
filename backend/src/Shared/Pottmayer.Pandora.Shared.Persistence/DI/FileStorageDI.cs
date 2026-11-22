using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Pandora.Shared.Persistence.Storage;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Shared.Persistence.DI;

public static class FileStorageDI
{
    /// <summary>
    /// Registers the module's <see cref="IFileStorage"/>: bytes in its own blob table (mapped with
    /// <see cref="FileBlobMapping.MapFileBlobs"/>), keyed by its <paramref name="databaseKey"/> so modules
    /// never write into each other's table. Inject it with
    /// <c>[FromKeyedServices(XModule.DatabaseKey)] IFileStorage</c>. An S3 backend later is another
    /// implementation registered under the same key.
    /// </summary>
    public static IServiceCollection AddPandoraDatabaseFileStorage(this IServiceCollection services, string databaseKey)
    {
        services.TryAddTransient<IFileBlobRepository, FileBlobRepository>();
        services.AddKeyedScoped<IFileStorage>(databaseKey, (sp, _) => new DatabaseFileStorage(
            databaseKey, sp.GetRequiredService<IUnitOfWorkFactory>(), sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
