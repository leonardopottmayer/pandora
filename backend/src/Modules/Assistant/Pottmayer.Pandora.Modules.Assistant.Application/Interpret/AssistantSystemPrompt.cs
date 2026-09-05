using System.Globalization;
using System.Text;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Interpret;

/// <summary>
/// Builds the one versioned system prompt that frames every interpretation: the reference clock (current
/// local time + IANA time zone + week start), the locale, the absolute-timestamp rule, and the few-shot
/// examples the commands carry. Everything time-sensitive is injected per call, so the model never has to
/// guess "now".
/// </summary>
internal static class AssistantSystemPrompt
{
    /// <summary>Bumped when the wording changes, so the audit trail can tie an interpretation to a prompt.</summary>
    public const int Version = 2;

    // Weekday names in the prompt are rendered in English, regardless of the user's locale.
    private static readonly CultureInfo PromptCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Build(
        DateTimeOffset localNow,
        string ianaTimeZone,
        DayOfWeek weekStartsOn,
        string locale,
        IReadOnlyList<AssistantCommandDescriptor> commands)
    {
        var nowText = localNow.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        var weekday = PromptCulture.DateTimeFormat.GetDayName(localNow.DayOfWeek);
        var weekStart = PromptCulture.DateTimeFormat.GetDayName(weekStartsOn);

        var sb = new StringBuilder();
        sb.AppendLine("You are Pandora's personal assistant. Your job is to interpret the user's sentence");
        sb.AppendLine("and, when it matches an available command, call the right tool with the correct");
        sb.AppendLine("arguments. You never execute anything yourself — you only choose the tool.");
        sb.AppendLine();
        sb.AppendLine("Current context (use it as the reference for resolving relative dates and times):");
        sb.AppendLine($"- Now: {nowText} ({weekday})");
        sb.AppendLine($"- User's time zone: {ianaTimeZone}");
        sb.AppendLine($"- Week starts on: {weekStart}");
        sb.AppendLine($"- User locale: {locale} (the user may write in this language; understand them regardless)");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Every date/time you pass to a tool must be an ABSOLUTE ISO-8601 timestamp with offset");
        sb.AppendLine("  (e.g. 2026-09-05T10:00:00-03:00). Resolve relative expressions (\"tomorrow\", \"next");
        sb.AppendLine("  Friday\", \"in 3 days\") against \"Now\" above.");
        sb.AppendLine("- Never invent information the user did not give. If an essential detail is missing or the");
        sb.AppendLine("  sentence is ambiguous, reply in prose with ONE short question instead of calling the tool.");
        sb.AppendLine("- If the sentence matches no command, reply in prose with a brief explanation.");
        sb.AppendLine("- Reply in English.");

        if (commands.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Available tools and examples:");
            foreach (var command in commands)
            {
                sb.AppendLine($"- {command.Name}: {command.Description}");
                foreach (var example in command.Examples)
                    sb.AppendLine($"  \"{example.Utterance}\" → {command.Name}({example.ArgumentsJson})");
            }
        }

        return sb.ToString();
    }
}
