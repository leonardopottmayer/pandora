using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;

public sealed class CreateReminderCommandHandler(
    IUnitOfWorkFactory factory, IUserPreferencesReader preferences, TimeProvider timeProvider)
    : CommandHandlerBase<CreateReminderCommand, ReminderDto>
{
    protected override async Task<Result<ReminderDto>> HandleAsync(CreateReminderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(ReminderErrors.TitleRequired);

        var timeZone = await TimeZoneResolver.ResolveAsync(preferences, input.UserId, input.TimeZone, ct);

        Reminder created;
        try
        {
            created = Reminder.Create(
                input.UserId, input.Title, input.Notes, input.RemindAt, timeZone, timeProvider, input.Rrule);
        }
        catch (FormatException ex)
        {
            // An RRULE outside the supported subset, or malformed — a write-time rejection.
            return Fail(ReminderErrors.InvalidRecurrence(ex.Message));
        }
        catch (ArgumentException ex)
        {
            // An unknown IANA time zone for a recurring reminder.
            return Fail(ReminderErrors.InvalidRecurrence(ex.Message));
        }

        await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            await reminders.AddAsync(created, token);
            return true;
        }, cancellationToken: ct);

        return Ok(created.ToDto());
    }
}
