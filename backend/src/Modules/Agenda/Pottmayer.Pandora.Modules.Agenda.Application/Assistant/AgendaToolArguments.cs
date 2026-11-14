using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>Arguments the Agenda's create tools share: a recurrence rule and a container picked by name.</summary>
internal static class AgendaToolArguments
{
    public const string RecurrenceSchema = """
            "recurrence": { "type": "string", "description": "Only when it repeats: an RFC 5545 RRULE without the 'RRULE:' prefix, e.g. FREQ=WEEKLY;BYDAY=TH, FREQ=MONTHLY;BYMONTHDAY=5, FREQ=DAILY;UNTIL=20261231T235959Z." }
        """;

    /// <summary>The rule the model sent, with a stray <c>RRULE:</c> prefix dropped; the command validates it.</summary>
    public static string? Recurrence(JsonElement arguments) =>
        OptionalString(arguments, "recurrence") is { } rule && !string.IsNullOrWhiteSpace(rule)
            ? rule.Trim().StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) ? rule.Trim()[6..] : rule.Trim()
            : null;

    /// <summary>" (repete)" / " (repeating)" for the confirmation and the reply.</summary>
    public static string Repeats(AssistantToolContext context, string? rrule) =>
        rrule is null ? "" : context.Text(" (repete)", " (repeating)");

    /// <summary>
    /// The container named in <paramref name="argument"/> among <paramref name="items"/>, else the default one
    /// (else the first). A name that matches none, or several, is the problem to answer with.
    /// </summary>
    public static (T? Match, string? Problem) PickNamedOrDefault<T>(
        AssistantToolContext context, JsonElement arguments, string argument, IReadOnlyList<T> items,
        Func<T, string> name, Func<T, bool> isDefault, string wherePt, string whereEn) where T : class =>
        OptionalString(arguments, argument) is { } wanted
            ? PickByTitle(context, items, name, wanted, wherePt, whereEn)
            : (items.FirstOrDefault(isDefault) ?? items.FirstOrDefault(), null);
}
