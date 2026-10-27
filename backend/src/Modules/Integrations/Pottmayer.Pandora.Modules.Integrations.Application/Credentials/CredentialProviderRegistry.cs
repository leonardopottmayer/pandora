using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Credentials;

/// <summary>
/// Resolves the <see cref="CredentialProviderDescriptor"/> for a provider key and secret kind. Backed by
/// whatever providers are registered in DI, so adding OpenAI is a registration, not a change here.
/// </summary>
public sealed class CredentialProviderRegistry
{
    private readonly IReadOnlyDictionary<string, CredentialProviderDescriptor> _providers;

    public CredentialProviderRegistry(IEnumerable<CredentialProviderDescriptor> providers) =>
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CredentialProviderDescriptor> All =>
        (IReadOnlyCollection<CredentialProviderDescriptor>)_providers.Values;

    /// <summary>Finds a provider that takes <paramref name="authKind"/> secrets.</summary>
    public bool TryGet(string provider, AuthKind authKind, out CredentialProviderDescriptor descriptor) =>
        _providers.TryGetValue(provider, out descriptor!) && descriptor.AuthKind == authKind;
}
