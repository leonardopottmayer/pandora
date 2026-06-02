using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.UserContext.AspNetCore.DI;
using Pottmayer.Tars.UserContext.DI;

namespace Pottmayer.Pandora.Shared.Infrastructure.DI;

public static class SharedInfrastructureDI
{
    public static IHostApplicationBuilder AddPandoraSharedInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddUserContext();

        return builder;
    }

    private static IServiceCollection AddUserContext(this IServiceCollection services)
    {
        services.AddTarsUserContextAccessor();
        services.AddTarsCurrentPrincipalAccessor();
        services.AddTarsClaimsUserResolver<UserData>();
        services.AddTarsDefaultUserContextFactory<UserData>();
        services.AddTarsUserContextAccessor<UserData>();

        return services;
    }
}
