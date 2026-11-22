using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

public sealed record DispatchDueTaskAlertsInput(int BatchSize);

/// <summary>Fires every task alert due now. Returns how many were dispatched.</summary>
public sealed class DispatchDueTaskAlertsCommand(DispatchDueTaskAlertsInput input)
    : CommandBase<DispatchDueTaskAlertsInput, int>(input);
