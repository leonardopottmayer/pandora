using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.DI;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddAdminPersistence(this IServiceCollection services)
    {
        services.AddTarsRelationalData<AdminDbContext>(AdminModule.DatabaseKey, (sp, descriptor) =>
            new DbContextOptionsBuilder<AdminDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));

        return services;
    }
}
