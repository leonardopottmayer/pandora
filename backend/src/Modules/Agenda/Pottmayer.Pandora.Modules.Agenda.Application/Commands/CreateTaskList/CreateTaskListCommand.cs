using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;

public sealed record CreateTaskListInput(Guid UserId, string Name, bool IsDefault = false, int Position = 0);

public sealed class CreateTaskListCommand(CreateTaskListInput input)
    : CommandBase<CreateTaskListInput, TaskListDto>(input);
