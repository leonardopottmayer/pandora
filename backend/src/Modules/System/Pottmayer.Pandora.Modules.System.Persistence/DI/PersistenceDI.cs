using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.System.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.System.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddSystemPersistence(this IServiceCollection services)
    {
        services.AddTarsData<SystemDbContext>(SystemModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<SystemDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
