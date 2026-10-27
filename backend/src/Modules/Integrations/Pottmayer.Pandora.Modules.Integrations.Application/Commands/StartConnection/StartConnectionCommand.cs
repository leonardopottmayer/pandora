using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.StartConnection;

/// <summary>
/// Begins an OAuth connection. <paramref name="RedirectAfter"/> is where the SPA should land once the
/// callback completes; it must be a relative path. <paramref name="Scopes"/> is optional — null falls
/// back to the provider's default feature set. <paramref name="AccountId"/> targets an existing account
/// (reconnect, or widen its scopes); null adds a new account.
/// </summary>
public sealed record StartConnectionInput(
    Guid UserId,
    string Provider,
    string RedirectAfter,
    IReadOnlyList<string>? Scopes,
    Guid? AccountId = null);

public sealed class StartConnectionCommand(StartConnectionInput input)
    : CommandBase<StartConnectionInput, string>(input);
