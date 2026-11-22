using System.Globalization;
using System.Text;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Interpret;

/// <summary>
/// Reads a short typed answer to a held call — "sim", "pode", "não, deixa" — as yes or no. Only a reply made
/// entirely of such words counts: "não, muda pra 11h" says more and goes to the model.
/// </summary>
internal static class ConfirmationReply
{
    private const int MaxWords = 4;

    private static readonly HashSet<string> Yes =
    [
        "sim", "s", "pode", "podes", "confirma", "confirmo", "confirmar", "isso", "ok", "okay", "beleza", "blz",
        "claro", "manda", "bora", "faz", "ser", "certo", "yes", "y", "yep", "sure", "go", "ahead", "do", "it",
    ];

    private static readonly HashSet<string> No =
    [
        "nao", "n", "cancela", "cancelar", "deixa", "pra", "la", "esquece", "precisa", "melhor", "negativo",
        "no", "nope", "cancel", "never", "mind", "dont",
    ];

    /// <summary>True for yes, false for no, null when the text is anything else.</summary>
    public static bool? Parse(string text)
    {
        var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > MaxWords)
            return null;
        if (words.All(Yes.Contains))
            return true;
        if (words.All(No.Contains))
            return false;
        return null;
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return sb.ToString();
    }
}
