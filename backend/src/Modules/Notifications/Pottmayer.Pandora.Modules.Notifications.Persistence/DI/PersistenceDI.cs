using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notifications.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Notifications.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddNotificationsPersistence(this IServiceCollection services)
    {
        services.AddTarsData<NotificationsDbContext>(NotificationsModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
