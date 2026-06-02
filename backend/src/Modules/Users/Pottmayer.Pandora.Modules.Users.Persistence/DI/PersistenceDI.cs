using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Users.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddUsersPersistence(this IServiceCollection services)
    {
        services.AddTarsData<UsersDbContext>(UsersModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<UsersDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
