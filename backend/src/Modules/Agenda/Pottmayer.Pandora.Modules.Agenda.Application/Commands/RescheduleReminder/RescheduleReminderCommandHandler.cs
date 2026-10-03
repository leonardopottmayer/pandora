using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.RescheduleReminder;

public sealed class RescheduleReminderCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<RescheduleReminderCommand, bool>
{
    private enum Outcome { Done, NotFound, Recurring }

    protected override async Task<Result<bool>> HandleAsync(RescheduleReminderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var outcome = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var reminder = await reminders.FindAsync(input.UserId, input.ReminderId, token);
            if (reminder is null)
                return Outcome.NotFound;
            if (reminder.IsRecurring)
                return Outcome.Recurring;

            reminder.Reschedule(input.At);
            await reminders.UpdateAsync(reminder, token);
            return Outcome.Done;
        }, cancellationToken: ct);

        return outcome switch
        {
            Outcome.Done => Ok(true),
            Outcome.Recurring => Fail(ReminderErrors.RecurringCannotBeRescheduled),
            _ => Fail(ReminderErrors.NotFound),
        };
    }
}
