using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A named container of events (doc agd001), the calendar-grid counterpart of a
/// <see cref="TaskList"/>. Exactly one per user is the default, guarded by a partial unique index;
/// archiving hides it without deleting. Deleting a calendar with live events is refused by the
/// application (archive instead), mirroring the task-list guard.
/// </summary>
public sealed class Calendar : AggregateRoot<Guid>, IAuditable
{
    private const int MaxNameLength = 200;

    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Display color (e.g. a hex string). Null ⇒ the client picks.</summary>
    public string? Color { get; private set; }

    public bool IsDefault { get; private set; }

    /// <summary>UI visibility toggle; does not affect alerts.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>IANA time zone. Defaults to UTC until Identity carries a user default.</summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary><see cref="CalendarOrigin.Local"/> is owned here; <see cref="CalendarOrigin.External"/> arrives with sync (Phase 5).</summary>
    public CalendarOrigin Origin { get; private set; }

    /// <summary>Soft hide; an archived calendar still owns its events.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Calendar() { }

    public static Calendar Create(
        Guid userId, string name, string? color, bool isDefault, string timeZone,
        CalendarOrigin origin, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A calendar needs a name.", nameof(name));

        var zone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;
        ResolveZone(zone); // validate the IANA id eagerly

        return new Calendar
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = Trim(name),
            Color = color,
            IsDefault = isDefault,
            IsVisible = true,
            TimeZone = zone,
            Origin = origin,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    public void Update(string name, string? color, bool isVisible, string timeZone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A calendar needs a name.", nameof(name));

        var zone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;
        ResolveZone(zone);

        Name = Trim(name);
        Color = color;
        IsVisible = isVisible;
        TimeZone = zone;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void Archive(TimeProvider timeProvider) => ArchivedAt ??= timeProvider.GetUtcNow();

    /// <summary>The calendar's zone as a <see cref="TimeZoneInfo"/>, throwing on an unknown IANA id.</summary>
    public TimeZoneInfo ResolveZone() => ResolveZone(TimeZone);

    private static TimeZoneInfo ResolveZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Unknown IANA time zone '{timeZone}'.", nameof(timeZone));
        }
    }

    private static string Trim(string name) => name.Trim() is { Length: > MaxNameLength } t ? t[..MaxNameLength] : name.Trim();
}
