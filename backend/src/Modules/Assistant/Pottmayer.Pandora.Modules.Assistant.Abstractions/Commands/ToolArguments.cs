using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// What the tools share: reading the model's arguments (throwing <see cref="ArgumentException"/>
/// or <see cref="FormatException"/>, which the pipeline turns into a rejected call) and finding the item
/// the user named by title — locally, so no title is ever sent to the model.
/// </summary>
public static class ToolArguments
{
    public static string RequiredString(JsonElement arguments, string name) =>
        OptionalString(arguments, name) ?? throw new ArgumentException($"The '{name}' argument is required.");

    public static string? OptionalString(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString())
            ? e.GetString()!.Trim()
            : null;

    public static DateTimeOffset RequiredInstant(JsonElement arguments, string name) =>
        OptionalInstant(arguments, name) ?? throw new ArgumentException($"The '{name}' argument is required.");

    public static DateTimeOffset? OptionalInstant(JsonElement arguments, string name) =>
        OptionalString(arguments, name) is { } text
            ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    /// <summary>
    /// A day or an instant: a bare date ("2026-09-05") is that day's start in the user's zone, with
    /// <c>HasTime</c> false; anything else is read as an ISO-8601 instant.
    /// </summary>
    public static (DateTimeOffset At, bool HasTime)? OptionalDayOrInstant(
        AssistantToolContext context, JsonElement arguments, string name)
    {
        if (OptionalString(arguments, name) is not { } text)
            return null;

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var midnight = day.ToDateTime(TimeOnly.MinValue);
            return (new DateTimeOffset(midnight, context.TimeZone.GetUtcOffset(midnight)), false);
        }

        return (DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), true);
    }

    /// <summary>A day or an instant in the user's zone and language ("05/09/2026" or "05/09/2026 às 10:00").</summary>
    public static string FormatDayOrInstant(AssistantToolContext context, DateTimeOffset at, bool hasTime)
    {
        if (hasTime)
            return context.FormatDateTime(at);

        var local = TimeZoneInfo.ConvertTime(at, context.TimeZone);
        return context.IsPortuguese
            ? local.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
            : local.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));
    }

    public static bool OptionalBool(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.True;

    /// <summary>
    /// The items whose title matches <paramref name="query"/>: the exact ones (ignoring case and accents)
    /// when there are any, otherwise those with a title word starting with each word of the query (words
    /// under three letters — "o", "de" — are ignored when the query has longer ones).
    /// </summary>
    public static IReadOnlyList<T> MatchByTitle<T>(IEnumerable<T> items, Func<T, string> title, string query)
    {
        var wanted = Normalize(query);
        var words = wanted.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.Length >= 3))
            words = [.. words.Where(w => w.Length >= 3)];
        var candidates = items.Select(i => (Item: i, Title: Normalize(title(i)))).ToList();

        var exact = candidates.Where(c => c.Title == wanted).Select(c => c.Item).ToList();
        return exact.Count > 0
            ? exact
            : [.. candidates
                .Where(c => words.All(w => c.Title.Split(' ').Any(t => t.StartsWith(w, StringComparison.Ordinal))))
                .Select(c => c.Item)];
    }

    /// <summary>
    /// Picks the one item the user named, or says why not in their language: nothing matched, or several
    /// did (listing up to five, so the user can say which).
    /// </summary>
    public static (T? Match, string? Problem) PickByTitle<T>(
        AssistantToolContext context, IEnumerable<T> items, Func<T, string> title, string query,
        string wherePt, string whereEn)
        where T : class
    {
        var matches = MatchByTitle(items, title, query);
        if (matches.Count == 1)
            return (matches[0], null);

        if (matches.Count == 0)
            return (null, context.Text(
                $"Não encontrei \"{query}\" entre {wherePt}.",
                $"I couldn't find \"{query}\" among {whereEn}."));

        var names = string.Join(", ", matches.Take(5).Select(m => $"\"{title(m)}\""));
        return (null, context.Text(
            $"\"{query}\" bate com mais de um item: {names}. Pode ser mais específico?",
            $"\"{query}\" matches more than one item: {names}. Can you be more specific?"));
    }

    /// <summary>
    /// The item the pipeline pinned into the call (<see cref="ListedRefs"/>) — the listed one the user pointed
    /// at by number, or the one found by name before a confirmation; null when nothing is pinned and the call
    /// names its target by words. Throws <see cref="ArgumentException"/>, in the user's language, when the
    /// number is not on the last list or stands for another kind of item.
    /// </summary>
    public static ListedItem? OptionalRef(AssistantToolContext context, JsonElement arguments, string kind)
    {
        var number = ListedRefs.Number(arguments);
        var id = OptionalString(arguments, ListedRefs.Id);
        if (id is null)
            return number is { } n ? throw new ArgumentException(ListedRefs.NotListed(context, n)) : null;

        var pinnedKind = OptionalString(arguments, ListedRefs.Kind);
        var label = OptionalString(arguments, ListedRefs.Label) ?? string.Empty;
        if (pinnedKind != kind)
            throw new ArgumentException(context.Text(
                $"O item {number} (\"{label}\") não é {KindPt(kind)}.",
                $"Item {number} (\"{label}\") is not a {kind}."));

        return new ListedItem(kind, Guid.Parse(id), label, OptionalInstant(arguments, ListedRefs.At));
    }

    /// <summary>
    /// How the user named the target of a call, for its confirmation question: the pinned item's title when
    /// there is one, otherwise their words in <paramref name="nameArgument"/>.
    /// </summary>
    public static string TargetName(JsonElement arguments, string nameArgument) =>
        (ListedRefs.IsPinned(arguments) ? OptionalString(arguments, ListedRefs.Label) : null)
        ?? OptionalString(arguments, nameArgument)
        ?? (ListedRefs.Number(arguments) is { } n ? $"#{n}" : throw new ArgumentException(
            $"Pass either '{ListedRefs.Ref}' or '{nameArgument}'."));

    /// <summary>
    /// The one item a call targets among <paramref name="items"/>: the listed one it pointed at by number
    /// (<see cref="OptionalRef"/>), else the one its words in <paramref name="nameArgument"/> name
    /// (<see cref="PickByTitle{T}"/>). Says why not, in the user's language, when there is no single match.
    /// </summary>
    public static (T? Match, string? Problem) PickTarget<T>(
        AssistantToolContext context, JsonElement arguments, string kind, string nameArgument,
        IEnumerable<T> items, Func<T, Guid> id, Func<T, string> title, string wherePt, string whereEn)
        where T : class
    {
        if (OptionalRef(context, arguments, kind) is { } pinned)
        {
            var match = items.FirstOrDefault(i => id(i) == pinned.Id);
            return match is not null
                ? (match, null)
                : (null, context.Text(
                    $"\"{pinned.Label}\" não está mais entre {wherePt}.",
                    $"\"{pinned.Label}\" is no longer among {whereEn}."));
        }

        var name = OptionalString(arguments, nameArgument)
            ?? throw new ArgumentException($"Pass either '{ListedRefs.Ref}' or '{nameArgument}'.");
        return PickByTitle(context, items, title, name, wherePt, whereEn);
    }

    private static string KindPt(string kind) => kind switch
    {
        "event" => "um evento",
        "task" => "uma tarefa",
        "reminder" => "um lembrete",
        "note" => "uma nota",
        _ => kind,
    };

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
