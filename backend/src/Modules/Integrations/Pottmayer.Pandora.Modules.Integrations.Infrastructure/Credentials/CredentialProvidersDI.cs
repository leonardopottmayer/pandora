using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Integrations.Infrastructure.Credentials;

/// <summary>
/// Registers the providers whose credential the user types in. They are pure catalog entries (no
/// endpoints, no server-side secret — the key or password is per user), so unlike Google OAuth they are
/// always available. Adding OpenAI is one more line here.
/// </summary>
public static class CredentialProvidersDI
{
    /// <summary>API-key providers (Gemini, …).</summary>
    public static IServiceCollection AddIntegrationsApiKeyProviders(this IServiceCollection services)
    {
        services.AddSingleton(new CredentialProviderDescriptor("gemini", "Google Gemini", AuthKind.ApiKey));
        return services;
    }

    /// <summary>Username + password providers: Titan Email and any IMAP/SMTP server.</summary>
    public static IServiceCollection AddIntegrationsPasswordProviders(this IServiceCollection services)
    {
        services.AddSingleton(new CredentialProviderDescriptor("titan", "Titan Email", AuthKind.Password));
        services.AddSingleton(new CredentialProviderDescriptor("imap", "IMAP", AuthKind.Password));
        return services;
    }
}
