using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddAssistantPersistence(this IServiceCollection services)
    {
        services.AddTarsData<AssistantDbContext>(AssistantModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<AssistantDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
