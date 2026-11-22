using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>Keyword retrieval for questions; ordinary palette search keeps its all-words semantics.</summary>
public static partial class NoteQuestionSearch
{
    public const int ExcerptLength = 4000;
    private static readonly HashSet<string> StopWords = new(
        ("a o as os e é de da do das dos em na no nas nos um uma uns umas que qual quais quando onde como por porque para " +
         "com sem meu minha meus minhas suas seus sobre foi são ser está estão tem tenho minhas notas " +
         "the a an and or of in on at to for with without what which when where how why is are was were be " +
         "my your our notes do does did can could would should tell me about").Split(' '), StringComparer.Ordinal);

    private static string[] Words(string question) => WordPattern().Matches(question.ToLowerInvariant())
        .Select(m => m.Value).Where(w => !StopWords.Contains(w)).Distinct().Take(12).ToArray();

    public static string ToTsQuery(string question) => string.Join(" | ", Words(question).Select(w => $"{w}:*"));

    /// <summary>A bounded passage around a match, including matches far beyond a page's opening.</summary>
    public static string Excerpt(string content, string question)
    {
        if (content.Length <= ExcerptLength) return content;
        var words = Words(question);
        var match = WordPattern().Matches(content).FirstOrDefault(m =>
            words.Any(word => m.Value.StartsWith(word, StringComparison.OrdinalIgnoreCase)));
        var start = match is null ? 0 : Math.Max(0, match.Index - 1000);
        start = Math.Min(start, content.Length - ExcerptLength);
        return (start > 0 ? "…" : "") + content.Substring(start, ExcerptLength) +
            (start + ExcerptLength < content.Length ? "…" : "");
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
