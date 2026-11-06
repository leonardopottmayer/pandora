using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Application.Authorization;
using Pottmayer.Pandora.Modules.Admin.Infrastructure.Jobs;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;

namespace Pottmayer.Pandora.Modules.Admin.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddAdminInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AdminOptions>()
            .Bind(builder.Configuration.GetSection(AdminOptions.SectionName));

        // The host-wide checkpoint. Every module declares its flags and permissions in its own
        // Application DI; these calls build the catalogs (validated at startup), the cached services
        // over Admin's stores, and the mediator behavior that checks every [RequiresPermission] request.
        builder.Services.AddTarsFeatureFlagCatalog();
        builder.Services.AddTarsFeatureFlagService();
        builder.Services.AddTarsPermissionCatalog();
        builder.Services.AddTarsPermissionChecker();
        builder.Services.AddTarsAuthorizationGate();
        builder.Services.AddTarsAuthorizationActorResolver<InputUserIdActorResolver>();
        builder.Services.AddTarsAuthorizationBehavior();

        builder.Services.AddHostedService<AdminBootstrapService>();

        return builder;
    }
}
