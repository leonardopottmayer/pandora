using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.RenameReminder;

public sealed class RenameReminderCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<RenameReminderCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RenameReminderCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(ReminderErrors.TitleRequired);

        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var reminder = await reminders.FindAsync(input.UserId, input.ReminderId, token);
            if (reminder is null)
                return false;

            reminder.Rename(input.Title);
            await reminders.UpdateAsync(reminder, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(ReminderErrors.NotFound);
    }
}
