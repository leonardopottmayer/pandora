using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.DispatchPending;

public sealed record DispatchPendingNotificationsInput(int BatchSize);

public sealed record DispatchPendingNotificationsResult(int Sent, int Failed, int Dead)
{
    public int Total => Sent + Failed + Dead;
}

public sealed class DispatchPendingNotificationsCommand(DispatchPendingNotificationsInput input)
    : CommandBase<DispatchPendingNotificationsInput, DispatchPendingNotificationsResult>(input);
