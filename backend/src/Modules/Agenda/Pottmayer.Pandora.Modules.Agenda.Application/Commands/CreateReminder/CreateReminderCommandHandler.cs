using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;

public sealed class CreateReminderCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateReminderCommand, ReminderDto>
{
    protected override async Task<Result<ReminderDto>> HandleAsync(CreateReminderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(ReminderErrors.TitleRequired);

        var reminder = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var created = Reminder.Create(
                input.UserId, input.Title, input.Notes, input.RemindAt, input.TimeZone ?? "UTC", timeProvider);
            await reminders.AddAsync(created, token);
            return created;
        }, cancellationToken: ct);

        return Ok(reminder.ToDto());
    }
}
