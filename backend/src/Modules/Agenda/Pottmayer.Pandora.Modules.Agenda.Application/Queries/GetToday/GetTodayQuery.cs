using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetToday;

/// <summary>
/// The unified day view: events, tasks and reminders for today, merged and ordered by time. <see cref="From"/>
/// and <see cref="To"/> (inclusive local days) widen it to a span of days; both default to today.
/// </summary>
public sealed record GetTodayInput(Guid UserId, DateOnly? From = null, DateOnly? To = null);

public sealed class GetTodayQuery(GetTodayInput input)
    : QueryBase<GetTodayInput, IReadOnlyList<TodayItemDto>>(input);
