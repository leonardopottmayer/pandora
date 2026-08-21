using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

public sealed record DispatchDueRemindersInput(int BatchSize);

/// <summary>Fires every reminder due now. Returns how many were dispatched.</summary>
public sealed class DispatchDueRemindersCommand(DispatchDueRemindersInput input)
    : CommandBase<DispatchDueRemindersInput, int>(input);
