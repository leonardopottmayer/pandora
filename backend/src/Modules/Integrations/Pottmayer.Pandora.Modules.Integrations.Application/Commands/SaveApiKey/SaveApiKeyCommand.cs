using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.SaveApiKey;

/// <summary>
/// Stores (or replaces) the user's API key for an <c>api_key</c> provider such as Gemini. Idempotent
/// per (user, provider): saving again overwrites the stored key. The key is protected before it is
/// persisted and never leaves the module in plaintext except through
/// <c>IExternalCredentialProvider.GetApiKeyAsync</c>.
/// </summary>
public sealed record SaveApiKeyInput(Guid UserId, string Provider, string ApiKey);

public sealed class SaveApiKeyCommand(SaveApiKeyInput input)
    : CommandBase<SaveApiKeyInput, bool>(input);
