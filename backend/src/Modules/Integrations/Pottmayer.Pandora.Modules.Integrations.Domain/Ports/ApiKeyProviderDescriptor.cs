namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

/// <summary>
/// Metadata for an <c>api_key</c> provider (OpenAI, Gemini, …). Unlike <see cref="IOAuthProvider"/> it
/// has no behaviour — there is no authorization flow — so it is pure catalog data: the provider key
/// (matching the <c>provider</c> column and the AI client factory key) and a friendly name for
/// settings. Adding a provider is a registration, not a change here.
/// </summary>
public sealed record ApiKeyProviderDescriptor(string Name, string DisplayName);
