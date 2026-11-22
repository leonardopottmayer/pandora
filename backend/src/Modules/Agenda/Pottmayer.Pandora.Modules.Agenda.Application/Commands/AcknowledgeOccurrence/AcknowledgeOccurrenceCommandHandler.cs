using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeOccurrence;

/// <summary>
/// Acknowledges one occurrence of a recurring reminder by stamping its dispatch row. The series is
/// untouched — the next occurrence still fires. Idempotent: a double tap of <c>Done</c> succeeds.
/// </summary>
public sealed class AcknowledgeOccurrenceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<AcknowledgeOccurrenceCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(AcknowledgeOccurrenceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var dispatches = context.AcquireRepository<IReminderDispatchRepository>();
            var dispatch = await dispatches.FindAsync(input.UserId, input.ReminderId, input.OccurrenceStartsAt, token);
            if (dispatch is null)
                return false;

            dispatch.Acknowledge(timeProvider);
            await dispatches.UpdateAsync(dispatch, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(ReminderErrors.NotFound);
    }
}
