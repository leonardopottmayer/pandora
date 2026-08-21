using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTaskList;

/// <summary>Patches a task list. Only the non-null fields are applied; <see cref="Archive"/> true archives it.</summary>
public sealed record UpdateTaskListInput(
    Guid UserId, Guid ListId, string? Name, int? Position, bool? IsDefault, bool Archive = false);

public sealed class UpdateTaskListCommand(UpdateTaskListInput input)
    : CommandBase<UpdateTaskListInput, TaskListDto>(input);
