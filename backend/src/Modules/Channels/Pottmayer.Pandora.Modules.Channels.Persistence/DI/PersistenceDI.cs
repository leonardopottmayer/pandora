using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.DI;

public static class PersistenceDI
{
    public static IServiceCollection AddChannelsPersistence(this IServiceCollection services)
    {
        services.AddTarsData<ChannelsDbContext>(ChannelsModule.Name, (sp, descriptor) =>
            new DbContextOptionsBuilder<ChannelsDbContext>()
                .UseNpgsql(descriptor.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())
                .Options);

        services.AddTarsDataRepositoriesFromAssemblies(typeof(PersistenceDI));
        return services;
    }
}
