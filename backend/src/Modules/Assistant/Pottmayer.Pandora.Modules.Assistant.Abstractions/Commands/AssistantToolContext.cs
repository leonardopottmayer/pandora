using System.Globalization;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// Who a tool runs for and how to speak to them: the user, their assistant <see cref="Locale"/> (every
/// user-facing sentence a tool returns is written in it) and their effective <see cref="TimeZone"/> (every
/// instant a tool shows is rendered in it). The pipeline builds one per interpretation or confirmation.
/// </summary>
public sealed record AssistantToolContext(Guid UserId, string Locale, TimeZoneInfo TimeZone)
{
    /// <summary>The locale used when a profile does not override it — the assistant speaks Portuguese first.</summary>
    public const string DefaultLocale = "pt-BR";

    /// <summary>True when the user speaks Portuguese; the assistant's copy exists in pt-BR and English.</summary>
    public bool IsPortuguese => Locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

    /// <summary>Picks the sentence for the user's language.</summary>
    public string Text(string portuguese, string english) => IsPortuguese ? portuguese : english;

    /// <summary>An instant in the user's zone, formatted for their language (e.g. "05/09/2026 às 10:00").</summary>
    public string FormatDateTime(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZone);
        return IsPortuguese
            ? local.ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"))
            : local.ToString("MMM d, yyyy 'at' HH:mm", CultureInfo.GetCultureInfo("en-US"));
    }
}
