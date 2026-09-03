using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Integrations.Application.ApiKeys;

/// <summary>
/// Resolves the <see cref="ApiKeyProviderDescriptor"/> for a provider key. Backed by whatever api_key
/// providers are registered in DI, so adding OpenAI is a registration, not a change here.
/// </summary>
public sealed class ApiKeyProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ApiKeyProviderDescriptor> _providers;

    public ApiKeyProviderRegistry(IEnumerable<ApiKeyProviderDescriptor> providers) =>
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ApiKeyProviderDescriptor> All =>
        (IReadOnlyCollection<ApiKeyProviderDescriptor>)_providers.Values;

    public bool TryGet(string provider, out ApiKeyProviderDescriptor descriptor) =>
        _providers.TryGetValue(provider, out descriptor!);
}
