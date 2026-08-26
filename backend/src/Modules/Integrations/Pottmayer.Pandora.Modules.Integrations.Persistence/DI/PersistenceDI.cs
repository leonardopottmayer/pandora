using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddIntegrationsPersistence(this IServiceCollection services)
    {
        services.AddTarsData<IntegrationsDbContext>(IntegrationsModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
