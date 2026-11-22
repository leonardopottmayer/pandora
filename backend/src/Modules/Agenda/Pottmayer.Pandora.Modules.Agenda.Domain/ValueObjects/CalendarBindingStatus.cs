using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>Whether a calendar binding syncs. <c>needs-reconnect</c> waits for the user to reconnect the account; <c>error</c> waits for "Sync now".</summary>
public sealed record CalendarBindingStatus : IDomainValue<CalendarBindingStatus>
{
    public string Value { get; }

    private CalendarBindingStatus(string value) => Value = value;

    public static readonly CalendarBindingStatus Ok = new("ok");
    public static readonly CalendarBindingStatus NeedsReconnect = new("needs-reconnect");
    public static readonly CalendarBindingStatus Error = new("error");

    public static readonly IReadOnlyList<CalendarBindingStatus> All = [Ok, NeedsReconnect, Error];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static CalendarBindingStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported calendar binding status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
