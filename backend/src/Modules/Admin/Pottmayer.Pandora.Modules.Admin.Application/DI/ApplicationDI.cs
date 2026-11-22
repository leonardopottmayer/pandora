using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Admin.Application.Authorization;
using Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;
using Pottmayer.Pandora.Modules.Admin.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Messaging.DI;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Admin.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // The stores behind the Tars flag service and permission checker. Read-only: the panel writes
        // the tables itself and then invalidates the caches.
        services.AddScoped<IFeatureFlagStore, FeatureFlagStore>();
        services.AddScoped<IAuthorizationStore, AuthorizationStore>();

        services.AddScoped<RoleAssigner>();
        services.AddScoped<AdminBootstrapper>();
        services.AddScoped<LastAdminGuard>();

        // Answers Identity's approval question: the bootstrap e-mails skip approval and hear about it.
        services.AddScoped<IAccountApprovalPolicy, BootstrapApprovalPolicy>();

        // The panel's own permissions. Admin has no feature flag: switching it off would lock everyone out.
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(AdminPermissions.Module)
                .Resource(AdminPermissions.Users.Resource).Read().Actions(AdminPermissions.Actions.Manage)
                .Resource(AdminPermissions.Roles.Resource).Read().Actions(AdminPermissions.Actions.Manage)
                .Resource(AdminPermissions.Features.Resource).Actions(AdminPermissions.Actions.Manage)
                .Resource(AdminPermissions.Audit.Resource).Read());

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        return services;
    }
}
