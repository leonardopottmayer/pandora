using System.Globalization;
using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// What the Agenda tools share: reading the model's arguments (throwing <see cref="ArgumentException"/>
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
