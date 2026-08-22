using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetAlerts;

/// <summary>Lists the alerts on one subject. <c>task</c> and <c>event</c> are supported, mirroring create.</summary>
public sealed class GetAlertsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAlertsQuery, IReadOnlyList<AlertDto>>
{
    protected override async Task<Result<IReadOnlyList<AlertDto>>> HandleAsync(
        GetAlertsQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        AlertSubjectType subjectType;
        if (string.Equals(input.SubjectType, "task", StringComparison.OrdinalIgnoreCase))
            subjectType = AlertSubjectType.Task;
        else if (string.Equals(input.SubjectType, "event", StringComparison.OrdinalIgnoreCase))
            subjectType = AlertSubjectType.Event;
        else
            return Fail(AlertErrors.UnsupportedSubjectType);

        var alerts = await factory.ExecuteAsync(AgendaModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IAlertRepository>();
            return await repo.GetBySubjectAsync(input.UserId, subjectType, input.SubjectId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<AlertDto> dtos = [.. alerts.Select(a => a.ToDto())];
        return Ok(dtos);
    }
}
