using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Oauth;

/// <summary>
/// Resolves the <see cref="IOAuthProvider"/> for a provider key. Backed by whatever providers are
/// registered in DI, so adding Microsoft is a registration, not a change here.
/// </summary>
public sealed class OAuthProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IOAuthProvider> _providers;

    public OAuthProviderRegistry(IEnumerable<IOAuthProvider> providers) =>
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Names => (IReadOnlyCollection<string>)_providers.Keys;

    public bool TryGet(string provider, out IOAuthProvider oauthProvider) =>
        _providers.TryGetValue(provider, out oauthProvider!);
}
