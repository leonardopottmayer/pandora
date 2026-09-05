using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Infrastructure.ApiKeys;
using Pottmayer.Pandora.Modules.Integrations.Infrastructure.Google;
using Pottmayer.Tars.Security.DataProtection.DI;

namespace Pottmayer.Pandora.Modules.Integrations.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddIntegrationsInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<IntegrationsOptions>()
            .Bind(builder.Configuration.GetSection(IntegrationsOptions.SectionName));

        // Secret protection (Tars): the key comes from configuration, never the database. Bound from
        // the Tars building-block section (Tars:Security:DataProtection), like every other Tars block.
        builder.AddTarsDataProtectionOptions();
        builder.Services.AddTarsSecretProtector();

        // api-key providers (Gemini, …): catalog entries only, no server-side secret, so always on.
        builder.Services.AddIntegrationsApiKeyProviders();

        // Google OAuth provider (Tars.Data-style guard): registered only when a client id is present,
        // so an unconfigured deployment simply has no Google provider rather than a half-wired one.
        builder.Services
            .AddOptions<GoogleOAuthOptions>()
            .Bind(builder.Configuration.GetSection(GoogleOAuthOptions.SectionName));

        if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{GoogleOAuthOptions.SectionName}:ClientId"]))
        {
            builder.Services.AddHttpClient<IOAuthProvider, GoogleOAuthProvider>();
        }

        return builder;
    }
}
