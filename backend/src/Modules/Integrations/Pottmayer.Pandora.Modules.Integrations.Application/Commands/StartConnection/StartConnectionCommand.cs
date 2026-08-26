using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.StartConnection;

/// <summary>
/// Begins an OAuth connection. <paramref name="RedirectAfter"/> is where the SPA should land once the
/// callback completes; it must be a relative path. <paramref name="Scopes"/> is optional — null falls
/// back to the provider's default feature set.
/// </summary>
public sealed record StartConnectionInput(
    Guid UserId,
    string Provider,
    string RedirectAfter,
    IReadOnlyList<string>? Scopes);

public sealed class StartConnectionCommand(StartConnectionInput input)
    : CommandBase<StartConnectionInput, string>(input);
