using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;

public sealed record RunStatementLifecycleInput(DateOnly Today);

/// <summary>
/// Daily job that keeps every active card's statements current: opens the current and next
/// reference-month statements if missing, then closes and re-syncs (paid/partially-paid/overdue)
/// every statement whose closing or due date has been reached. Returns how many statements changed.
/// </summary>
public sealed class RunStatementLifecycleCommand(RunStatementLifecycleInput input)
    : CommandBase<RunStatementLifecycleInput, int>(input);
