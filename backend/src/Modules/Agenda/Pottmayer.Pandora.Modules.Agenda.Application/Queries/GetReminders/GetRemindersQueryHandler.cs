using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;

public sealed class GetRemindersQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRemindersQuery, IReadOnlyList<ReminderDto>>
{
    protected override async Task<Result<IReadOnlyList<ReminderDto>>> HandleAsync(
        GetRemindersQuery request, CancellationToken cancellationToken)
    {
        var reminders = await factory.ExecuteAsync(AgendaModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IReminderRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<ReminderDto> dtos = [.. reminders.Select(r => r.ToDto())];
        return Ok(dtos);
    }
}
