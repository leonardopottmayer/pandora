using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;

/// <param name="Today">One date for every user (tests, a manual run); null takes each user's own day in their zone.</param>
public sealed record RunStatementLifecycleInput(DateOnly? Today = null);

/// <summary>
/// Daily job that keeps every active card's statements current: opens the current and next
/// reference-month statements if missing, then closes and re-syncs (paid/partially-paid/overdue)
/// every statement whose closing or due date has been reached. Returns how many statements changed.
/// </summary>
public sealed class RunStatementLifecycleCommand(RunStatementLifecycleInput input)
    : CommandBase<RunStatementLifecycleInput, int>(input);
