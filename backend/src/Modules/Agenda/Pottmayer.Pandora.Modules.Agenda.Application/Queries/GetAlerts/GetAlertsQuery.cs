using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetAlerts;

public sealed record GetAlertsInput(Guid UserId, string SubjectType, Guid SubjectId);

public sealed class GetAlertsQuery(GetAlertsInput input)
    : QueryBase<GetAlertsInput, IReadOnlyList<AlertDto>>(input);
