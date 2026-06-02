using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services)
    {
        services.AddTarsData<IdentityDbContext>(IdentityModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
