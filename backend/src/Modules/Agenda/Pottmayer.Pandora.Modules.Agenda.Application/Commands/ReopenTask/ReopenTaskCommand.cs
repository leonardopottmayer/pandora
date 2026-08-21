using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.ReopenTask;

public sealed record ReopenTaskInput(Guid UserId, Guid TaskId);

/// <summary>Reopens a task, clearing its completion stamp.</summary>
public sealed class ReopenTaskCommand(ReopenTaskInput input)
    : CommandBase<ReopenTaskInput, TaskDto>(input);
