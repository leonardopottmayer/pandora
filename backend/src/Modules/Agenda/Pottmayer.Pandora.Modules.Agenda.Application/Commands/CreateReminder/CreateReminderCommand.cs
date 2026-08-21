using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;

public sealed record CreateReminderInput(
    Guid UserId, string Title, string? Notes, DateTimeOffset RemindAt, string? TimeZone);

public sealed class CreateReminderCommand(CreateReminderInput input)
    : CommandBase<CreateReminderInput, ReminderDto>(input);
