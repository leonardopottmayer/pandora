using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>
    /// Wires the Communications module's infrastructure. Only the options binding exists for now;
    /// providers (Gmail), background sync and outbound ports arrive in later phases. The hook exists
    /// so the Host registration mirrors the other modules.
    /// </summary>
    public static IHostApplicationBuilder AddCommunicationsInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<CommunicationsOptions>()
            .Bind(builder.Configuration.GetSection(CommunicationsOptions.SectionName));

        return builder;
    }
}
