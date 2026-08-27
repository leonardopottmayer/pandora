using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;

public sealed class SnoozeReminderCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SnoozeReminderCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SnoozeReminderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var reminder = await reminders.FindAsync(input.UserId, input.ReminderId, token);
            if (reminder is null)
                return false;

            reminder.Snooze(input.SnoozedUntil);
            await reminders.UpdateAsync(reminder, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(ReminderErrors.NotFound);
    }
}
