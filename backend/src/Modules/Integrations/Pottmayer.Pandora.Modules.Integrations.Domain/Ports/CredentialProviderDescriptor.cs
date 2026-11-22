using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

/// <summary>
/// Metadata for a provider whose credential the user types in — an API key (OpenAI, Gemini) or a
/// username + password (Titan, generic IMAP). Unlike <see cref="IOAuthProvider"/> it has no behaviour —
/// there is no authorization flow — so it is pure catalog data: the provider key (matching the
/// <c>provider</c> column), a friendly name for settings, and which kind of secret it takes. Adding a
/// provider is a registration, not a change here.
/// </summary>
public sealed record CredentialProviderDescriptor(string Name, string DisplayName, AuthKind AuthKind);
