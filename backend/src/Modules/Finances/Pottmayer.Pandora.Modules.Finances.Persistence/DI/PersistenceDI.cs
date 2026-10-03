using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.DI;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.DI;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddFinancesPersistence(this IServiceCollection services)
    {
        services.AddTarsRelationalData<FinancesDbContext>(FinancesModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<FinancesDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));

        // Attachment bytes in the finances.fin018_file_blob table, keyed by this module's database key.
        services.AddPandoraDatabaseFileStorage(FinancesModule.DatabaseKey);
        return services;
    }
}
