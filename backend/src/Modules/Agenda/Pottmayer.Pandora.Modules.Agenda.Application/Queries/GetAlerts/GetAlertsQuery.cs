using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetAlerts;

public sealed record GetAlertsInput(Guid UserId, string SubjectType, Guid SubjectId);

[RequiresPermission(AgendaPermissions.Alerts.Read)]
public sealed class GetAlertsQuery(GetAlertsInput input)
    : QueryBase<GetAlertsInput, IReadOnlyList<AlertDto>>(input);
