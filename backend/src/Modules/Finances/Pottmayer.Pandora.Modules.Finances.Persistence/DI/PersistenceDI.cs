using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddFinancesPersistence(this IServiceCollection services)
    {
        services.AddTarsData<FinancesDbContext>(FinancesModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<FinancesDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
