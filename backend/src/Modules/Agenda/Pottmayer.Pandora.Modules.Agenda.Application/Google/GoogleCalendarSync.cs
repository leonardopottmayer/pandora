using System.Collections.Concurrent;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>One binding syncs at a time; a second caller skips instead of waiting.</summary>
public sealed class CalendarSyncGates
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gates = new();

    public SemaphoreSlim For(Guid bindingId) => gates.GetOrAdd(bindingId, _ => new SemaphoreSlim(1, 1));
}

/// <summary>
/// One sync cycle of a binding, Google → Pandora. Reads Google first (the calendar's default reminders,
/// the events, the instances of series Pandora cannot expand), then applies everything in one unit of
/// work: events, cancelled and edited occurrences, alerts, links, and the binding's new cursor.
/// </summary>
/// <remarks>
/// A full listing runs on the first cycle, after Google's 410, and every <see cref="FullSyncEvery"/>; it
/// starts <see cref="History"/> back and removes what Google no longer has. The incremental listing
/// brings only changes, cancellations included.
/// </remarks>
public sealed class GoogleCalendarSync(
    IUnitOfWorkFactory factory, IGoogleCalendarClient google, CalendarSyncGates gates, TimeProvider timeProvider)
{
    /// <summary>How far back a full listing starts.</summary>
    public static readonly TimeSpan History = TimeSpan.FromDays(365);

    /// <summary>How far ahead a series Pandora cannot expand is brought as its instances.</summary>
    public static readonly TimeSpan ExpansionWindow = TimeSpan.FromDays(365);

    /// <summary>A full listing this often renews the window of expanded series and heals drift.</summary>
    public static readonly TimeSpan FullSyncEvery = TimeSpan.FromDays(30);

    /// <summary>Runs one cycle. Fails only when the binding does not exist; a Google failure is recorded on the binding.</summary>
    public async Task<Result<bool>> SyncAsync(Guid bindingId, CancellationToken ct)
    {
        var gate = gates.For(bindingId);
        if (!await gate.WaitAsync(TimeSpan.Zero, ct))
            return Result<bool>.Success(false);

        try
        {
            var binding = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
                context.AcquireRepository<ICalendarBindingRepository>().GetAsync(bindingId, token), cancellationToken: ct);
            if (binding is null)
                return Result<bool>.Failure([GoogleCalendarErrors.BindingNotFound]);

            var fetched = await FetchAsync(binding, ct);
            if (!fetched.IsSuccess)
            {
                await RecordFailureAsync(bindingId, fetched.Errors[0], ct);
                return Result<bool>.Success(false);
            }

            await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
                new Applier(context, binding, fetched.Value!, timeProvider).ApplyAsync(token), cancellationToken: ct);
            return Result<bool>.Success(true);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Result<Fetched>> FetchAsync(CalendarBinding binding, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var calendar = await google.GetCalendarAsync(binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId, ct);
        if (!calendar.IsSuccess)
            return Result<Fetched>.Failure(calendar.Errors);

        var full = binding.NeedsFullSync(now, FullSyncEvery);
        var timeMin = now - History;
        var listing = await google.ListEventsAsync(
            binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId, full ? null : binding.SyncToken, full ? timeMin : null, ct);

        if (!full && !listing.IsSuccess && listing.Errors[0].Code == GoogleCalendarErrors.SyncTokenExpired.Code)
        {
            full = true;
            listing = await google.ListEventsAsync(
                binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId, null, timeMin, ct);
        }

        if (!listing.IsSuccess)
            return Result<Fetched>.Failure(listing.Errors);

        var expansions = new Dictionary<string, IReadOnlyList<GoogleEvent>>();
        foreach (var series in listing.Value!.Events.Where(e => e.RecurringEventId is null && !e.IsCancelled
                                                                && !GoogleEventMapper.IsRecurrenceSupported(e.Recurrence)))
        {
            var instances = await google.ListInstancesAsync(
                binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId, series.Id, timeMin, now + ExpansionWindow, ct);
            if (!instances.IsSuccess)
                return Result<Fetched>.Failure(instances.Errors);
            expansions[series.Id] = instances.Value!;
        }

        return Result<Fetched>.Success(new Fetched(calendar.Value!, listing.Value, expansions, full, timeMin, now));
    }

    private Task RecordFailureAsync(Guid bindingId, Error error, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var repository = context.AcquireRepository<ICalendarBindingRepository>();
            var binding = await repository.GetAsync(bindingId, token);
            if (binding is null)
                return;

            if (GoogleCalendarErrors.NeedsReconnect(error))
                binding.MarkNeedsReconnect(error.Message);
            else
                binding.RecordFailure(error.Message);
            await repository.UpdateAsync(binding, token);
        }, cancellationToken: ct);

    private sealed record Fetched(
        GoogleCalendarListEntry Calendar,
        GoogleEventListing Listing,
        IReadOnlyDictionary<string, IReadOnlyList<GoogleEvent>> Expansions,
        bool IsFull,
        DateTimeOffset TimeMin,
        DateTimeOffset Now);

    /// <summary>Applies one fetched cycle inside a unit of work.</summary>
    private sealed class Applier
    {
        private readonly IEventRepository events;
        private readonly IEventOccurrenceOverrideRepository overrides;
        private readonly IAlertRepository alerts;
        private readonly IEventLinkRepository linkRepository;
        private readonly ICalendarBindingRepository bindings;
        private readonly CalendarBinding binding;
        private readonly Fetched fetched;
        private readonly TimeProvider timeProvider;
        private readonly string zone;

        private readonly Dictionary<string, EventLink> links = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, Event> eventsById = [];
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        /// <summary>Overrides touched this cycle: a lookup must see one added a moment ago, which the database does not have yet.</summary>
        private readonly Dictionary<(Guid EventId, DateTimeOffset Start), EventOccurrenceOverride> touchedOverrides = [];

        public Applier(IDataContext context, CalendarBinding binding, Fetched fetched, TimeProvider timeProvider)
        {
            events = context.AcquireRepository<IEventRepository>();
            overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();
            alerts = context.AcquireRepository<IAlertRepository>();
            linkRepository = context.AcquireRepository<IEventLinkRepository>();
            bindings = context.AcquireRepository<ICalendarBindingRepository>();
            this.binding = binding;
            this.fetched = fetched;
            this.timeProvider = timeProvider;
            zone = fetched.Calendar.TimeZone ?? "UTC";
        }

        public async Task ApplyAsync(CancellationToken ct)
        {
            foreach (var link in await linkRepository.GetByBindingAsync(binding.Id, ct))
                links[link.RemoteEventId] = link;
            foreach (var ev in await events.GetLiveByIdsAsync([.. links.Values.Select(l => l.EventId)], ct))
                eventsById[ev.Id] = ev;

            // Series and single events first, so an exception finds its series.
            foreach (var google in fetched.Listing.Events.Where(e => e.RecurringEventId is null))
                await ApplySeriesAsync(google, ct);
            foreach (var google in fetched.Listing.Events.Where(e => e.RecurringEventId is not null))
                await ApplyExceptionAsync(google, ct);

            if (fetched.IsFull)
                await RemoveVanishedAsync(ct);

            var current = await bindings.GetAsync(binding.Id, ct) ?? binding;
            current.RecordSuccess(fetched.Listing.NextSyncToken, fetched.IsFull, fetched.Now);
            await bindings.UpdateAsync(current, ct);
        }

        private async Task ApplySeriesAsync(GoogleEvent google, CancellationToken ct)
        {
            seen.Add(google.Id);

            if (google.IsCancelled)
            {
                await RemoveAsync(google.Id, ct);
                await RemoveInstancesAsync(google.Id, keep: NoKeep, ct);
                return;
            }

            if (fetched.Expansions.TryGetValue(google.Id, out var instances))
            {
                // Pandora cannot expand this rule: its occurrences come as single events.
                await RemoveAsync(google.Id, ct);
                var kept = new HashSet<string>(StringComparer.Ordinal);
                foreach (var instance in instances.Where(i => !i.IsCancelled))
                {
                    kept.Add(instance.Id);
                    seen.Add(instance.Id);
                    await UpsertAsync(instance, seriesRemoteId: google.Id, ct);
                }
                await RemoveInstancesAsync(google.Id, kept, ct);
                return;
            }

            await RemoveInstancesAsync(google.Id, keep: NoKeep, ct);
            var ev = await UpsertAsync(google, seriesRemoteId: null, ct);
            if (ev is not null)
                await CancelExcludedAsync(ev, GoogleEventMapper.Map(google, zone).ExcludedStarts, ct);
        }

        private async Task ApplyExceptionAsync(GoogleEvent google, CancellationToken ct)
        {
            var seriesId = google.RecurringEventId!;

            // A series Pandora expands itself: the exception is an override of one occurrence.
            if (links.TryGetValue(seriesId, out var seriesLink) && seriesLink.SeriesRemoteId is null
                && eventsById.TryGetValue(seriesLink.EventId, out var series))
            {
                if (GoogleEventMapper.OriginalStart(google, zone) is not { } original)
                    return;

                var occurrence = await FindOverrideAsync(series.Id, original, ct);
                var isNew = occurrence is null;
                occurrence ??= EventOccurrenceOverride.Create(series.Id, series.UserId, original, timeProvider);

                if (google.IsCancelled)
                {
                    occurrence.Cancel();
                }
                else
                {
                    var mapped = GoogleEventMapper.Map(google, zone);
                    occurrence.Edit(mapped.StartsAt, mapped.EndsAt, mapped.Title, mapped.Description, mapped.Location);
                }

                touchedOverrides[(series.Id, original)] = occurrence;
                if (isNew)
                    await overrides.AddAsync(occurrence, ct);
                else
                    await overrides.UpdateAsync(occurrence, ct);
                return;
            }

            // A series brought as instances: the exception is one of those single events.
            if (links.Values.Any(l => l.SeriesRemoteId == seriesId))
            {
                seen.Add(google.Id);
                if (google.IsCancelled)
                    await RemoveAsync(google.Id, ct);
                else
                    await UpsertAsync(google, seriesRemoteId: seriesId, ct);
            }

            // Otherwise the series is older than the listed window: nothing in Pandora to change.
        }

        /// <summary>Creates or overwrites the event linked to <paramref name="google"/>, with its alerts.</summary>
        private async Task<Event?> UpsertAsync(GoogleEvent google, string? seriesRemoteId, CancellationToken ct)
        {
            MappedEvent mapped;
            try
            {
                mapped = GoogleEventMapper.Map(google, zone);
            }
            catch (FormatException)
            {
                return null; // no usable start: nothing to show
            }

            var rrule = seriesRemoteId is null ? mapped.Rrule : null;
            links.TryGetValue(google.Id, out var link);
            Event? ev = null;
            if (link is not null)
                eventsById.TryGetValue(link.EventId, out ev);

            try
            {
                if (ev is null)
                {
                    if (link is not null)
                    {
                        await linkRepository.RemoveAsync(link, ct);
                        links.Remove(google.Id);
                    }

                    ev = Event.Create(binding.UserId, binding.CalendarId, mapped.Title, mapped.Description, mapped.Location,
                        mapped.Url, mapped.StartsAt, mapped.EndsAt, mapped.IsAllDay, mapped.TimeZone, rrule, mapped.Status, timeProvider);
                    await events.AddAsync(ev, ct);
                    eventsById[ev.Id] = ev;

                    // The link is inserted once, after its alerts set the reminders hash: an insert then an
                    // update of the same new row is what EF must not be asked to order.
                    link = EventLink.Create(binding.Id, ev.Id, google.Id, seriesRemoteId, timeProvider);
                    link.Touch(google.Etag, google.Updated);
                    await SyncAlertsAsync(ev, link, google, ct);
                    await linkRepository.AddAsync(link, ct);
                    links[google.Id] = link;
                }
                else
                {
                    ev.ApplyRemote(mapped.Title, mapped.Description, mapped.Location, mapped.Url, mapped.StartsAt,
                        mapped.EndsAt, mapped.IsAllDay, mapped.TimeZone, rrule, mapped.Status);
                    await events.UpdateAsync(ev, ct);
                    link!.Touch(google.Etag, google.Updated);
                    await SyncAlertsAsync(ev, link, google, ct);
                    await linkRepository.UpdateAsync(link, ct);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return null; // an event Pandora cannot hold (unknown zone, bad rule): skip it, keep the cycle
            }

            return ev;
        }

        /// <summary>Rewrites the event's alerts when its Google reminders changed. The caller saves the link.</summary>
        private async Task SyncAlertsAsync(Event ev, EventLink link, GoogleEvent google, CancellationToken ct)
        {
            var offsets = GoogleEventMapper.AlertOffsets(google, fetched.Calendar.DefaultReminders);
            var hash = GoogleEventMapper.HashOffsets(offsets);
            if (link.RemindersHash == hash)
                return;

            foreach (var alert in await alerts.GetBySubjectAsync(ev.UserId, AlertSubjectType.Event, ev.Id, ct))
                await alerts.RemoveAsync(alert, ct);
            foreach (var offset in offsets)
                await alerts.AddAsync(Alert.Create(ev.UserId, AlertSubjectType.Event, ev.Id, offset, null, timeProvider), ct);

            link.SetRemindersHash(hash);
        }

        private async Task CancelExcludedAsync(Event ev, IReadOnlyList<DateTimeOffset> excluded, CancellationToken ct)
        {
            foreach (var start in excluded)
            {
                var existing = await FindOverrideAsync(ev.Id, start, ct);
                if (existing is null)
                {
                    var created = EventOccurrenceOverride.Create(ev.Id, ev.UserId, start, timeProvider);
                    created.Cancel();
                    touchedOverrides[(ev.Id, start)] = created;
                    await overrides.AddAsync(created, ct);
                }
                else if (!existing.IsCancelled)
                {
                    existing.Cancel();
                    await overrides.UpdateAsync(existing, ct);
                }
            }
        }

        private async Task<EventOccurrenceOverride?> FindOverrideAsync(Guid eventId, DateTimeOffset start, CancellationToken ct) =>
            touchedOverrides.TryGetValue((eventId, start.ToUniversalTime()), out var touched)
                ? touched
                : await overrides.FindAsync(eventId, start, ct);

        /// <summary>Soft-deletes the event linked to a Google id, drops its alerts and the link.</summary>
        private async Task RemoveAsync(string remoteEventId, CancellationToken ct)
        {
            if (!links.Remove(remoteEventId, out var link))
                return;

            if (eventsById.Remove(link.EventId, out var ev))
            {
                ev.Delete(timeProvider);
                await events.UpdateAsync(ev, ct);
                foreach (var alert in await alerts.GetBySubjectAsync(ev.UserId, AlertSubjectType.Event, ev.Id, ct))
                    await alerts.RemoveAsync(alert, ct);
            }

            await linkRepository.RemoveAsync(link, ct);
        }

        private static readonly IReadOnlySet<string> NoKeep = new HashSet<string>();

        private async Task RemoveInstancesAsync(string seriesRemoteId, IReadOnlySet<string> keep, CancellationToken ct)
        {
            var stale = links.Values
                .Where(l => l.SeriesRemoteId == seriesRemoteId && !keep.Contains(l.RemoteEventId))
                .Select(l => l.RemoteEventId)
                .ToList();
            foreach (var remoteId in stale)
                await RemoveAsync(remoteId, ct);
        }

        /// <summary>
        /// After a full listing, an event Google no longer returned is gone, unless it ended before the
        /// listing's window: those are history the listing does not cover, and they stay.
        /// </summary>
        private async Task RemoveVanishedAsync(CancellationToken ct)
        {
            var vanished = links.Values.Where(l => !seen.Contains(l.RemoteEventId)).ToList();
            foreach (var link in vanished)
            {
                if (eventsById.TryGetValue(link.EventId, out var ev) && LastEnd(ev) is { } end && end < fetched.TimeMin)
                    continue;
                await RemoveAsync(link.RemoteEventId, ct);
            }
        }

        private static DateTimeOffset? LastEnd(Event ev) =>
            ev.IsRecurring ? ev.RecurrenceEndsAt : ev.EndsAt;
    }
}
