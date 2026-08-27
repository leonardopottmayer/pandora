using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;

/// <summary>
/// Expands the events overlapping the window into concrete occurrences (not rows), overlaying overrides
/// (cancellations vanish, edits win), DST-aware in each event's own zone. The window is capped so a
/// pathological rule cannot expand unbounded.
/// </summary>
public sealed class GetEventsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetEventsQuery, IReadOnlyList<EventOccurrenceDto>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(366);

    protected override async Task<Result<IReadOnlyList<EventOccurrenceDto>>> HandleAsync(
        GetEventsQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        if (input.To < input.From)
            return Fail(EventErrors.RangeInvalid);
        if (input.To - input.From > MaxRange)
            return Fail(EventErrors.RangeTooLarge);

        var occurrences = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var events = context.AcquireRepository<IEventRepository>();
            var overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();

            var rows = await events.GetOverlappingAsync(
                input.UserId,
                input.CalendarIds is { Count: > 0 } ids ? ids : null,
                input.From, input.To, ct);
            if (rows.Count == 0)
                return new List<EventOccurrence>();

            var overrideRows = await overrides.GetByEventsAsync([.. rows.Select(e => e.Id)], ct);
            var overridesByEvent = overrideRows
                .GroupBy(o => o.EventId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<EventOccurrenceOverride>)[.. g]);

            var expanded = new List<EventOccurrence>();
            foreach (var ev in rows)
            {
                var forEvent = overridesByEvent.TryGetValue(ev.Id, out var list) ? list : [];
                expanded.AddRange(EventExpander.Expand(ev, forEvent, input.From, input.To));
            }

            return expanded;
        }, cancellationToken: cancellationToken);

        IReadOnlyList<EventOccurrenceDto> dtos =
            [.. occurrences.OrderBy(o => o.StartsAt).Select(o => o.ToDto())];
        return Ok(dtos);
    }
}
