using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;

public sealed record GetEventInput(Guid UserId, Guid EventId);

[RequiresPermission(AgendaPermissions.Events.Read)]
public sealed class GetEventQuery(GetEventInput input)
    : QueryBase<GetEventInput, EventDto>(input);
