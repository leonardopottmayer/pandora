using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>Which way a calendar binding syncs: <c>read</c> (Google → Pandora) or <c>read-write</c> (both ways).</summary>
public sealed record CalendarBindingDirection : IDomainValue<CalendarBindingDirection>
{
    public string Value { get; }

    private CalendarBindingDirection(string value) => Value = value;

    public static readonly CalendarBindingDirection Read = new("read");
    public static readonly CalendarBindingDirection ReadWrite = new("read-write");

    public static readonly IReadOnlyList<CalendarBindingDirection> All = [Read, ReadWrite];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static CalendarBindingDirection FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported calendar binding direction value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
