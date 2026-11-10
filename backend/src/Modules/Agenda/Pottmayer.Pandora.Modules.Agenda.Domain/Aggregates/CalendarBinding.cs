using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// One Pandora <see cref="Calendar"/> bound to one Google calendar of one Integrations account (doc
/// agd009). It carries the sync cursor: Google's <see cref="SyncToken"/>, null until the first full
/// sync. Only Google → Pandora exists today, so a bound calendar is read-only in Pandora.
/// </summary>
public sealed class CalendarBinding : AggregateRoot<Guid>, IAuditable
{
    /// <summary>After this many failed cycles in a row the binding stops until "Sync now".</summary>
    public const int MaxConsecutiveFailures = 5;

    private const int MaxErrorLength = 1000;

    public Guid UserId { get; private set; }
    public Guid CalendarId { get; private set; }
    public Guid ExternalAccountId { get; private set; }
    public string RemoteCalendarId { get; private set; } = string.Empty;

    /// <summary>Google's cursor for the next incremental listing. Null ⇒ the next cycle lists everything.</summary>
    public string? SyncToken { get; private set; }

    /// <summary>When the last full listing finished; a full listing also renews the window of expanded series.</summary>
    public DateTimeOffset? FullSyncedAt { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }
    public CalendarBindingStatus Status { get; private set; } = CalendarBindingStatus.Ok;
    public int FailureCount { get; private set; }
    public string? LastError { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private CalendarBinding() { }

    public static CalendarBinding Create(
        Guid userId, Guid calendarId, Guid externalAccountId, string remoteCalendarId, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(remoteCalendarId))
            throw new ArgumentException("A binding needs the remote calendar's id.", nameof(remoteCalendarId));

        return new CalendarBinding
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CalendarId = calendarId,
            ExternalAccountId = externalAccountId,
            RemoteCalendarId = remoteCalendarId,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>True when the next cycle must list every event instead of the changes since <see cref="SyncToken"/>.</summary>
    public bool NeedsFullSync(DateTimeOffset now, TimeSpan fullSyncEvery) =>
        SyncToken is null || FullSyncedAt is null || now - FullSyncedAt.Value >= fullSyncEvery;

    /// <summary>A cycle finished: keep Google's next cursor and clear the failures.</summary>
    public void RecordSuccess(string? nextSyncToken, bool wasFullSync, DateTimeOffset now)
    {
        SyncToken = nextSyncToken;
        if (wasFullSync)
            FullSyncedAt = now;
        LastSyncedAt = now;
        Status = CalendarBindingStatus.Ok;
        FailureCount = 0;
        LastError = null;
    }

    /// <summary>A cycle failed for a reason that may pass; after <see cref="MaxConsecutiveFailures"/> the binding stops.</summary>
    public void RecordFailure(string error)
    {
        LastError = Trim(error);
        FailureCount++;
        if (FailureCount >= MaxConsecutiveFailures)
            Status = CalendarBindingStatus.Error;
    }

    /// <summary>The account lost its access (revoked, or the token lacks the calendar scope): wait for the user.</summary>
    public void MarkNeedsReconnect(string error)
    {
        LastError = Trim(error);
        Status = CalendarBindingStatus.NeedsReconnect;
    }

    /// <summary>"Sync now": a stopped binding tries again from where it was.</summary>
    public void Resume()
    {
        Status = CalendarBindingStatus.Ok;
        FailureCount = 0;
    }

    /// <summary>Google said the cursor expired (410): the next listing is a full one.</summary>
    public void ResetCursor() => SyncToken = null;

    private static string Trim(string error) => error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
}
