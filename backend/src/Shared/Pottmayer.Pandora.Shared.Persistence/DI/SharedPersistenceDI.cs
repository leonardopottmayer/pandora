using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Shared.Persistence.Interceptors;
using Pottmayer.Tars.Data.Relational.DI;

namespace Pottmayer.Pandora.Shared.Persistence.DI;

public static class SharedPersistenceDI
{
    public static IHostApplicationBuilder AddPandoraSharedPersistence(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddTarsDataContextAccessor();
        services.AddTarsRelationalConfigurationConnectionResolver();
        services.AddTarsDataContextFactory();
        services.AddTarsRelationalUnitOfWorkFactory();

        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        return builder;
    }
}
