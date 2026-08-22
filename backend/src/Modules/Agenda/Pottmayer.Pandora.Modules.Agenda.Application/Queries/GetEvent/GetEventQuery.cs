using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;

public sealed record GetEventInput(Guid UserId, Guid EventId);

public sealed class GetEventQuery(GetEventInput input)
    : QueryBase<GetEventInput, EventDto>(input);
