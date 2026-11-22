using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Application.Credentials;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddIntegrationsApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        services.AddScoped<OAuthProviderRegistry>();
        services.AddScoped<CredentialProviderRegistry>();

        // The ports other modules consume. (IExternalAccountUsageSource goes the other way: each
        // consumer module registers its own implementation.)
        services.AddScoped<IExternalCredentialProvider, ExternalCredentialProvider>();
        services.AddScoped<IExternalAccountReader, ExternalAccountReader>();

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(IntegrationsFeatures.Integrations));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(IntegrationsPermissions.Module, feature: IntegrationsFeatures.Integrations)
                .Resource(IntegrationsPermissions.Accounts.Resource).Crud());

        return services;
    }
}
