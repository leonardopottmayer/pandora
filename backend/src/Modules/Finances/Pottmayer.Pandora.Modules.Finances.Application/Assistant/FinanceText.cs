using System.Globalization;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>How the Finances tools write money and days in the user's language.</summary>
internal static class FinanceText
{
    public static CultureInfo Culture(AssistantToolContext context) =>
        CultureInfo.GetCultureInfo(context.IsPortuguese ? "pt-BR" : "en-US");

    /// <summary>"R$ 1.234,56" (or "USD 12.00" for another currency).</summary>
    public static string Money(AssistantToolContext context, decimal amount, string currency) =>
        currency == "BRL"
            ? $"R$ {amount.ToString("N2", Culture(context))}"
            : $"{currency} {amount.ToString("N2", Culture(context))}";

    /// <summary>"05/09/2026" or "Sep 5, 2026".</summary>
    public static string Day(AssistantToolContext context, DateOnly day) =>
        day.ToString(context.IsPortuguese ? "dd/MM/yyyy" : "MMM d, yyyy", Culture(context));

    /// <summary>"05/09" or "Sep 5" — for lines inside a list that already says the period.</summary>
    public static string ShortDay(AssistantToolContext context, DateOnly day) =>
        day.ToString(context.IsPortuguese ? "dd/MM" : "MMM d", Culture(context));
}
