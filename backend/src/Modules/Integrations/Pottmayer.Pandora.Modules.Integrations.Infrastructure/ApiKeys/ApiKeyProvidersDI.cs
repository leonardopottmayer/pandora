using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Integrations.Infrastructure.ApiKeys;

/// <summary>
/// Registers the api_key providers this deployment supports. They are pure catalog entries (no
/// endpoints, no server-side secret — the key is per user), so unlike Google OAuth they are always
/// available. Adding OpenAI is one more line here.
/// </summary>
public static class ApiKeyProvidersDI
{
    public static IServiceCollection AddIntegrationsApiKeyProviders(this IServiceCollection services)
    {
        services.AddSingleton(new ApiKeyProviderDescriptor("gemini", "Google Gemini"));
        return services;
    }
}
