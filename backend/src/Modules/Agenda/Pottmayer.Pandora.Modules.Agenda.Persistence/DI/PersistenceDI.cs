using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddAgendaPersistence(this IServiceCollection services)
    {
        services.AddTarsData<AgendaDbContext>(AgendaModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<AgendaDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
