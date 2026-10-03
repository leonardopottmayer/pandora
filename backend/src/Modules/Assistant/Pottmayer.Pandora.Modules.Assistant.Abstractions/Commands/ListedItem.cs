namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// One numbered line of a list a tool showed the user, in display order, so a follow-up can point at it by
/// number ("cancela o 2"). <see cref="Kind"/> says what it is ("event", "task", "reminder", "note") — the
/// model sees only the kinds and numbers, never <see cref="Label"/>. <see cref="At"/> pins an occurrence of
/// a recurring item (an event's start), when the line stood for one.
/// </summary>
public sealed record ListedItem(string Kind, Guid Id, string Label, DateTimeOffset? At = null);
