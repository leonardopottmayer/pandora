using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

/// <summary>
/// The scan that fires due event alerts. Because an event is calculated, never stored, this path
/// <b>expands</b> the event's occurrences within the grace window (like the recurring-reminder sweep),
/// unlike the task path where each task row is a single concrete anchor. For every occurrence whose
/// firing instant (occurrence start + offset) lands in the window it publishes one
/// <see cref="NotifyUserRequested"/> — category <c>agenda.event</c>, template <c>agenda.event.due</c>,
/// no action buttons (an event has no complete/snooze) — inside the unit of work that records the fire,
/// then publishes after commit.
///
/// <para>Idempotency is the shared per-anchor ledger (agd008), unique on
/// <c>(alert, occurrence_starts_at)</c> where the anchor is the occurrence's on-grid start: an anchor
/// with a row is skipped, so re-running a tick — or restarting mid-tick — never double-fires. A
/// cancelled occurrence (an EXDATE override) does not fire.</para>
/// </summary>
public sealed class DispatchDueEventAlertsCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    IOptions<AgendaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchDueEventAlertsCommand, int>
{
    private const string TemplateKey = "agenda.event.due";

    // Older than this after its firing instant, a dispatch is flagged late (delivered from the grace window).
    private static readonly TimeSpan LateThreshold = TimeSpan.FromMinutes(1);

    private readonly AgendaOptions _options = options.Value;

    protected override async Task<Result<int>> HandleAsync(DispatchDueEventAlertsCommand request, CancellationToken ct)
    {
        var batchSize = request.Input.BatchSize;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _options.SweepGraceMinutes));

        var events = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var alerts = context.AcquireRepository<IAlertRepository>();
            var dispatches = context.AcquireRepository<IAlertDispatchRepository>();
            var eventRepo = context.AcquireRepository<IEventRepository>();
            var overrideRepo = context.AcquireRepository<IEventOccurrenceOverrideRepository>();

            var now = timeProvider.GetUtcNow();
            var windowStart = now - grace;

            var enabled = await alerts.GetEnabledEventAlertsAsync(batchSize, token);
            if (enabled.Count == 0)
                return [];

            var subjects = (await eventRepo.GetLiveByIdsAsync([.. enabled.Select(a => a.SubjectId).Distinct()], token))
                .ToDictionary(e => e.Id);

            var toPublish = new List<NotifyUserRequested>();
            foreach (var alert in enabled)
            {
                if (!subjects.TryGetValue(alert.SubjectId, out var ev))
                    continue;

                var overrides = await overrideRepo.GetByEventAsync(ev.Id, token);
                var due = EventAlertExpansion.DueOccurrences(ev, alert.OffsetMinutes, overrides, windowStart, now);
                if (due.Count == 0)
                    continue;

                foreach (var occurrence in due)
                {
                    if (await dispatches.ExistsAsync(alert.Id, occurrence, token))
                        continue;

                    var correlationId = Guid.CreateVersion7();
                    toPublish.Add(BuildNotification(ev, occurrence, correlationId, now));

                    var firing = alert.FiringInstant(occurrence);
                    var isLate = now - firing > LateThreshold;
                    await dispatches.AddAsync(
                        AlertDispatch.Record(alert.Id, alert.UserId, occurrence, correlationId, isLate, timeProvider),
                        token);
                }
            }

            // Published inside the unit of work: the alert-dispatch ledger row and its notification
            // commit together, so a fire is never recorded without its notification, nor sent for a
            // fire that rolled back (transactional outbox).
            foreach (var evt in toPublish)
                await bus.PublishAsync(evt, token);

            return toPublish;
        }, cancellationToken: ct);

        return Ok(events.Count);
    }

    private static NotifyUserRequested BuildNotification(
        Event ev, DateTimeOffset occurrenceStartsAt, Guid correlationId, DateTimeOffset now) =>
        new(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            UserId: ev.UserId,
            Category: AgendaCategories.Event,
            TemplateKey: TemplateKey,
            Locale: null, // Channels renders in the user's channel locale.
            Channels: null, // Channels resolves from the user's preference.
            Payload: new Dictionary<string, string>
            {
                ["title"] = ev.Title,
                ["location"] = ev.Location ?? string.Empty,
                ["url"] = ev.Url ?? string.Empty,
                ["startsAt"] = occurrenceStartsAt.ToString("O"),
            },
            CorrelationId: correlationId,
            Buttons: null); // An event has no complete/snooze semantics — no action buttons.
}
