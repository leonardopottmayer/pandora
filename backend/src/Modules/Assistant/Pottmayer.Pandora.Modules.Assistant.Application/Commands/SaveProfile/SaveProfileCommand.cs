using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.SaveProfile;

/// <summary>
/// Creates or replaces the user's assistant profile (one per user). The API key is not part of this — it
/// is stored separately in Integrations.
/// </summary>
public sealed record SaveProfileInput(
    Guid UserId,
    string Provider,
    string Model,
    bool IsEnabled,
    string? LocaleOverride,
    string ConfirmationLevel);

public sealed class SaveProfileCommand(SaveProfileInput input)
    : CommandBase<SaveProfileInput, bool>(input);
