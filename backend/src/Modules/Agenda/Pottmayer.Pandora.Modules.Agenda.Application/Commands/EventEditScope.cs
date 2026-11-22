namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands;

/// <summary>
/// The reach of an event edit or delete (doc §5.4): a single occurrence, this occurrence and every one
/// after it (which splits the series), or the whole series.
/// </summary>
public enum EventEditScope
{
    This,
    ThisAndFuture,
    All,
}

public static class EventEditScopeParser
{
    /// <summary>Parses the <c>?scope=</c> query value; null/empty defaults to <see cref="EventEditScope.All"/>.</summary>
    public static bool TryParse(string? value, out EventEditScope scope)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "all":
                scope = EventEditScope.All;
                return true;
            case "this":
                scope = EventEditScope.This;
                return true;
            case "this-and-future":
                scope = EventEditScope.ThisAndFuture;
                return true;
            default:
                scope = EventEditScope.All;
                return false;
        }
    }
}
