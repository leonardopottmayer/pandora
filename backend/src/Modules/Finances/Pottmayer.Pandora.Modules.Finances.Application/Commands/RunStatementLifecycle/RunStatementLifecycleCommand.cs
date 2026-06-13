using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;

public sealed record RunStatementLifecycleInput(DateOnly Today);

public sealed class RunStatementLifecycleCommand(RunStatementLifecycleInput input)
    : CommandBase<RunStatementLifecycleInput, int>(input);
