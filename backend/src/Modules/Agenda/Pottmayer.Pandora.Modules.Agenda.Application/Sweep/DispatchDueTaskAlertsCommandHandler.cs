using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Tasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

/// <summary>
/// The scan that fires due task alerts. Its scan root is the enabled task alerts; the anchor of each is
/// its task's <c>DueAt</c>, and the firing instant is that anchor plus the alert's offset. It publishes
/// one <see cref="NotifyUserRequested"/> per firing (a "Concluir" button that completes the task) inside
/// the same unit of work that records the fire, then publishes after commit.
///
/// <para>Idempotency is the per-anchor ledger (agd008), unique on <c>(alert, occurrence)</c>: an anchor
/// with a row is skipped, so re-running a tick — or restarting mid-tick — never double-fires. No RRULE
/// expansion happens here: a recurring task is materialized one instance at a time, so each task row is
/// a single concrete anchor.</para>
/// </summary>
public sealed class DispatchDueTaskAlertsCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    IOptions<AgendaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchDueTaskAlertsCommand, int>
{
    private const string TemplateKey = "agenda.task.due";

    // Older than this after its firing instant, a dispatch is flagged late (delivered from the grace window).
    private static readonly TimeSpan LateThreshold = TimeSpan.FromMinutes(1);

    private readonly AgendaOptions _options = options.Value;

    protected override async Task<Result<int>> HandleAsync(DispatchDueTaskAlertsCommand request, CancellationToken ct)
    {
        var batchSize = request.Input.BatchSize;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _options.SweepGraceMinutes));

        var events = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var alerts = context.AcquireRepository<IAlertRepository>();
            var dispatches = context.AcquireRepository<IAlertDispatchRepository>();
            var tasks = context.AcquireRepository<ITaskRepository>();

            var now = timeProvider.GetUtcNow();
            var windowStart = now - grace;

            var enabled = await alerts.GetEnabledTaskAlertsAsync(batchSize, token);
            if (enabled.Count == 0)
                return [];

            var subjects = (await tasks.GetLiveDueByIdsAsync([.. enabled.Select(a => a.SubjectId).Distinct()], token))
                .ToDictionary(t => t.Id);

            var toPublish = new List<NotifyUserRequested>();
            foreach (var alert in enabled)
            {
                if (!subjects.TryGetValue(alert.SubjectId, out var task) || task.DueAt is not { } anchor)
                    continue;

                var firing = alert.FiringInstant(anchor);
                if (firing < windowStart || firing > now)
                    continue;

                if (await dispatches.ExistsAsync(alert.Id, anchor, token))
                    continue;

                var correlationId = Guid.CreateVersion7();
                toPublish.Add(BuildNotification(task, correlationId, now));

                var isLate = now - firing > LateThreshold;
                await dispatches.AddAsync(
                    AlertDispatch.Record(alert.Id, alert.UserId, anchor, correlationId, isLate, timeProvider), token);
            }

            return toPublish;
        }, cancellationToken: ct);

        // After commit: the fire is only true once it is durably recorded.
        foreach (var evt in events)
            await bus.PublishAsync(evt, ct);

        return Ok(events.Count);
    }

    private static NotifyUserRequested BuildNotification(TaskItem task, Guid correlationId, DateTimeOffset now) =>
        new(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            UserId: task.UserId,
            Category: AgendaCategories.Task,
            TemplateKey: TemplateKey,
            Locale: null, // Channels renders in the user's channel locale.
            Channels: null, // Channels resolves from the user's preference.
            Payload: new Dictionary<string, string>
            {
                ["title"] = task.Title,
                ["notes"] = task.Notes ?? string.Empty,
            },
            CorrelationId: correlationId,
            Buttons:
            [
                new NotificationButton("agenda", "task_done", "✓ Concluir", TaskButtonPayload.For(task.Id)),
            ]);
}
