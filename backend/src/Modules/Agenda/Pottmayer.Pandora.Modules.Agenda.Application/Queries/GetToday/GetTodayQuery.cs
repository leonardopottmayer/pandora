using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetToday;

/// <summary>The unified day view: events, tasks and reminders for today, merged and ordered by time.</summary>
public sealed record GetTodayInput(Guid UserId);

public sealed class GetTodayQuery(GetTodayInput input)
    : QueryBase<GetTodayInput, IReadOnlyList<TodayItemDto>>(input);
