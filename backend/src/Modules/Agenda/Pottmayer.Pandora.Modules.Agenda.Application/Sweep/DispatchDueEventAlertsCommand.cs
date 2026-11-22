using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

public sealed record DispatchDueEventAlertsInput(int BatchSize);

/// <summary>Fires the event alerts due this tick. Returns how many notifications were published.</summary>
public sealed class DispatchDueEventAlertsCommand(DispatchDueEventAlertsInput input)
    : CommandBase<DispatchDueEventAlertsInput, int>(input);
