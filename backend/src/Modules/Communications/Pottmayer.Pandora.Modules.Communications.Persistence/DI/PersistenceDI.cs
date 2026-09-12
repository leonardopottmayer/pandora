using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddCommunicationsPersistence(this IServiceCollection services)
    {
        services.AddTarsData<CommunicationsDbContext>(CommunicationsModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<CommunicationsDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));

        return services;
    }
}
