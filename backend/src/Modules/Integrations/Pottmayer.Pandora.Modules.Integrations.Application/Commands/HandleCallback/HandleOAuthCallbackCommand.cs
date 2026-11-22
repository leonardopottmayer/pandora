using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.HandleCallback;

/// <summary>
/// The provider redirected back with an authorization <paramref name="Code"/> and the
/// <paramref name="State"/> this system issued. The state is the only thing authenticating the call.
/// Returns the relative SPA path to redirect the browser to.
/// </summary>
public sealed record HandleOAuthCallbackInput(string Provider, string Code, string State);

public sealed class HandleOAuthCallbackCommand(HandleOAuthCallbackInput input)
    : CommandBase<HandleOAuthCallbackInput, string>(input);
