using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Application.Credentials;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Tars.Core.Mediator.DI;

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

        return services;
    }
}
