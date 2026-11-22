using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.PurgeInboundUpdates;

/// <summary>Clears raw inbound payloads received before <paramref name="RawOlderThan"/>.</summary>
public sealed record PurgeInboundUpdatesInput(DateTimeOffset RawOlderThan);

public sealed class PurgeInboundUpdatesCommand(PurgeInboundUpdatesInput input)
    : CommandBase<PurgeInboundUpdatesInput, int>(input);
