using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.Jobs;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.Stores;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;
using Pottmayer.Tars.Security.Identity.AspNetCore.DI;
using Pottmayer.Tars.Security.Identity.DI;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddIdentityInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddIdentityOptions();
        builder.Services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        builder.Services.AddRefreshTokenStore();
        builder.Services.AddIdentityTokenServices();
        builder.Services.AddIdentityAuthentication();
        builder.Services.AddHostedService<RefreshTokenPurgeBackgroundService>();

        return builder;
    }

    private static IHostApplicationBuilder AddIdentityOptions(this IHostApplicationBuilder builder)
    {
        builder.AddTarsIdentityOptions();
        builder.AddTarsIdentityAspNetCoreOptions();

        return builder;
    }

    private static IServiceCollection AddRefreshTokenStore(this IServiceCollection services)
    {
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        return services;
    }

    private static IServiceCollection AddIdentityTokenServices(this IServiceCollection services)
    {
        services.AddTarsIdentityJwtTokenIssuer();
        services.AddTarsIdentityJwtTokenValidator();
        services.AddTarsIdentityRefreshTokenService();
        services.AddTarsIdentityTokenRevocationService();
        services.AddTarsIdentityTokenDeliveryPolicy();
        services.AddTarsIdentityAspNetCoreTokenTransport();
        services.AddTarsIdentityAspNetCoreJwtBearer();
        services.AddTarsIdentityInMemoryTokenRevocationStore();

        return services;
    }

    private static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityJwtBearerExtensions.DefaultJwtScheme;
            options.DefaultChallengeScheme    = IdentityJwtBearerExtensions.DefaultJwtScheme;
            options.DefaultSignInScheme       = IdentityJwtBearerExtensions.DefaultJwtScheme;
        })
        .AddJwtBearer(IdentityJwtBearerExtensions.DefaultJwtScheme, options =>
        {
            options.ConfigureTarsIdentityJwtBearerEvents();
            options.ConfigureTarsIdentityProblemResponses(
                unauthorizedError: Error.Unauthorized("Identity.NotAuthenticated", "Authentication required."),
                forbiddenError:    Error.Forbidden("Identity.AccessDenied", "Access denied."));
        });

        services.AddAuthorization();

        return services;
    }
}
