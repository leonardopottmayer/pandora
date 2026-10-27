using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.SaveApiKey;

/// <summary>
/// Adds an API key for an <c>api-key</c> provider such as Gemini as a <b>new</b> account — a user can
/// hold several keys per provider, and a key has no identity to match an existing one by. Replacing the
/// key of an existing account is <c>ReplaceSecret</c>. The key is protected before it is persisted and
/// never leaves the module in plaintext except through <c>IExternalCredentialProvider.GetApiKeyAsync</c>.
/// <paramref name="Label"/> is optional; it defaults to the provider name plus the key's last characters.
/// Returns the new account's id.
/// </summary>
public sealed record SaveApiKeyInput(Guid UserId, string Provider, string ApiKey, string? Label);

public sealed class SaveApiKeyCommand(SaveApiKeyInput input)
    : CommandBase<SaveApiKeyInput, Guid>(input);
