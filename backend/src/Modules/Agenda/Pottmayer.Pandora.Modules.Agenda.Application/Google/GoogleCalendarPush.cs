using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Pandora → Google, the first half of a cycle of a <c>read-write</c> binding: every event whose link is
/// pending goes to Google (inserted, patched or deleted), then the link takes Google's new etag, which is
/// how the reading half recognises its own change coming back (echo suppression).
/// </summary>
/// <remarks>
/// A patch carries the etag last seen. Google's 412 means the event changed there too: the newer edit
/// wins (Google's <c>updated</c> against the time of Pandora's edit), the other version goes to the
/// conflict log. When Google wins, the binding's cursor is reset so the reading half lists everything and
/// brings Google's version over Pandora's.
/// </remarks>
internal sealed class GoogleCalendarPush(IUnitOfWorkFactory factory, IGoogleCalendarClient google, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// <see cref="Fatal"/>: the account cannot be used, stop the cycle. <see cref="Failure"/>: some events did
    /// not go and stay pending for the next cycle.
    /// </summary>
    public sealed record Outcome(Error? Fatal, string? Failure);

    private sealed record Item(
        EventLink Link, Event? Event, IReadOnlyList<int> AlertOffsets, IReadOnlyList<EventOccurrenceOverride> Occurrences);

    /// <summary>What happened in Google to one item, for the save.</summary>
    private sealed record Done(
        GoogleEvent? Remote = null,
        string? RemindersHash = null,
        bool Removed = false,
        bool GoogleWon = false,
        bool KeepPending = false,
        SyncConflict? Conflict = null);

    public async Task<Outcome> PushAsync(CalendarBinding binding, CancellationToken ct)
    {
        var items = await LoadAsync(binding.Id, ct);

        string? failure = null;
        foreach (var item in items)
        {
            var result = await PushOneAsync(binding, item, ct);
            if (!result.IsSuccess)
            {
                var error = result.Errors[0];
                if (GoogleCalendarErrors.NeedsReconnect(error))
                    return new Outcome(error, null);
                failure ??= $"\"{item.Event?.Title}\" did not reach Google: {error.Message}";
                continue;
            }

            await SaveAsync(binding.Id, item.Link, result.Value!, ct);
        }

        return new Outcome(null, failure);
    }

    private Task<IReadOnlyList<Item>> LoadAsync(Guid bindingId, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var links = await context.AcquireRepository<IEventLinkRepository>().GetPendingAsync(bindingId, token);
            if (links.Count == 0)
                return (IReadOnlyList<Item>)[];

            var alerts = context.AcquireRepository<IAlertRepository>();
            var overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();
            var events = (await context.AcquireRepository<IEventRepository>()
                    .GetByIdsAsync([.. links.Select(l => l.EventId)], token))
                .ToDictionary(e => e.Id);

            var items = new List<Item>();
            foreach (var link in links)
            {
                events.TryGetValue(link.EventId, out var ev);
                if (ev is null || ev.DeletedAt is not null)
                {
                    items.Add(new Item(link, ev, [], []));
                    continue;
                }

                var offsets = (await alerts.GetBySubjectAsync(ev.UserId, AlertSubjectType.Event, ev.Id, token))
                    .Where(a => a.IsEnabled).Select(a => a.OffsetMinutes).ToList();
                var occurrences = ev.IsRecurring && link.SeriesRemoteId is null
                    ? await overrides.GetByEventAsync(ev.Id, token)
                    : [];
                items.Add(new Item(link, ev, offsets, occurrences));
            }
            return (IReadOnlyList<Item>)items;
        }, cancellationToken: ct);

    private async Task<Result<Done>> PushOneAsync(CalendarBinding binding, Item item, CancellationToken ct)
    {
        var (link, ev) = (item.Link, item.Event);
        var (user, account, calendar) = (binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId);

        if (ev is null || ev.DeletedAt is not null)
        {
            if (!link.IsInGoogle)
                return Result<Done>.Success(new Done(Removed: true));
            var deleted = await google.DeleteEventAsync(user, account, calendar, link.RemoteEventId, ct);
            return deleted.IsSuccess ? Result<Done>.Success(new Done(Removed: true)) : Result<Done>.Failure(deleted.Errors);
        }

        var body = GoogleEventMapper.ToGoogle(ev, item.AlertOffsets, id: null, isInstance: link.SeriesRemoteId is not null);
        GoogleEvent? overwritten = null;
        Result<GoogleEvent> sent;

        if (!link.IsInGoogle)
        {
            sent = await google.InsertEventAsync(user, account, calendar, body with { Id = link.RemoteEventId }, ct);
            // An earlier insert got there before its answer was lost: the event is ours, update it.
            if (Is(sent, GoogleCalendarErrors.EventExists))
                sent = await google.PatchEventAsync(user, account, calendar, link.RemoteEventId, body, null, ct);
        }
        else
        {
            sent = await google.PatchEventAsync(user, account, calendar, link.RemoteEventId, body, link.Etag, ct);
            if (Is(sent, GoogleCalendarErrors.EventChanged))
            {
                var current = await google.GetEventAsync(user, account, calendar, link.RemoteEventId, ct);
                if (!current.IsSuccess)
                    return Is(current, GoogleCalendarErrors.EventNotFound)
                        ? Result<Done>.Success(new Done()) // deleted in Google: the reading half removes it
                        : Result<Done>.Failure(current.Errors);

                if (current.Value!.Updated is { } googleAt && googleAt > link.PendingPushAt)
                    return Result<Done>.Success(new Done(GoogleWon: true,
                        Conflict: ConflictOf(binding, ev, SyncConflictSide.Pandora, JsonSerializer.Serialize(body, Json))));

                overwritten = current.Value;
                sent = await google.PatchEventAsync(user, account, calendar, link.RemoteEventId, body, null, ct);
            }
        }

        if (Is(sent, GoogleCalendarErrors.EventNotFound))
            return Result<Done>.Success(new Done()); // deleted in Google meanwhile: the reading half removes it
        if (!sent.IsSuccess)
            return Result<Done>.Failure(sent.Errors);

        var keepPending = !await PushOccurrencesAsync(binding, link.RemoteEventId, ev, item.Occurrences, ct);
        return Result<Done>.Success(new Done(
            sent.Value,
            GoogleEventMapper.HashReminders(body.Reminders!),
            KeepPending: keepPending,
            Conflict: overwritten is null
                ? null
                : ConflictOf(binding, ev, SyncConflictSide.Google, JsonSerializer.Serialize(overwritten, Json))));
    }

    /// <summary>
    /// A series' edited and cancelled occurrences, as patches and deletes of its Google instances. False
    /// when one failed: the series stays pending and goes again, occurrences included.
    /// </summary>
    /// <remarks>ponytail: every occurrence on every push of its series; per-occurrence pending if series grow many edits.</remarks>
    private async Task<bool> PushOccurrencesAsync(
        CalendarBinding binding, string seriesRemoteId, Event series, IReadOnlyList<EventOccurrenceOverride> occurrences,
        CancellationToken ct)
    {
        foreach (var occurrence in occurrences)
        {
            var instanceId = GoogleEventMapper.InstanceId(seriesRemoteId, series, occurrence.OriginalStartsAt);
            if (occurrence.IsCancelled)
            {
                if (!(await google.DeleteEventAsync(binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId, instanceId, ct)).IsSuccess)
                    return false;
                continue;
            }

            var patched = await google.PatchEventAsync(binding.UserId, binding.ExternalAccountId, binding.RemoteCalendarId,
                instanceId, GoogleEventMapper.ToGoogle(occurrence, series), null, ct);
            if (!patched.IsSuccess && !Is(patched, GoogleCalendarErrors.EventNotFound))
                return false;
        }
        return true;
    }

    private Task SaveAsync(Guid bindingId, EventLink pushed, Done done, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var links = context.AcquireRepository<IEventLinkRepository>();
            var link = await links.FindByEventAsync(pushed.EventId, token);
            if (link is null)
                return;

            if (done.Conflict is not null)
                await context.AcquireRepository<ISyncConflictRepository>().AddAsync(done.Conflict, token);

            // Edited again while this push was on the wire: that edit still has to go.
            var editedSince = link.PendingPushAt > pushed.PendingPushAt;

            if (done.Removed && !editedSince)
            {
                await links.RemoveAsync(link, token);
                return;
            }

            if (done.Remote is { } remote)
            {
                link.Touch(remote.Etag, remote.Updated);
                link.SetRemindersHash(done.RemindersHash!);
            }
            if (!editedSince && !done.KeepPending)
                link.ClearPending();
            await links.UpdateAsync(link, token);

            if (done.GoogleWon)
            {
                var bindings = context.AcquireRepository<ICalendarBindingRepository>();
                if (await bindings.GetAsync(bindingId, token) is { } binding)
                {
                    binding.ResetCursor();
                    await bindings.UpdateAsync(binding, token);
                }
            }
        }, cancellationToken: ct);

    private SyncConflict ConflictOf(CalendarBinding binding, Event ev, SyncConflictSide losing, string version) =>
        SyncConflict.Create(binding.UserId, binding.Id, ev.Id, ev.Title, losing, version, timeProvider);

    private static bool Is<T>(Result<T> result, Error error) =>
        !result.IsSuccess && result.Errors[0].Code == error.Code;
}
