using System.Text;
using System.Text.RegularExpressions;
using RegexEngine = System.Text.RegularExpressions.Regex;

namespace Pottmayer.Pandora.Modules.Files.Agent;

/// <summary>
/// The matchers a filter can use. Each tests an item's name, except a glob holding a <c>/</c>, which tests
/// the path from the root (<c>**/backup/**</c>).
/// </summary>
public static class NameMatcher
{
    public const string Extension = "extension";
    public const string Glob = "glob";
    public const string StartsWith = "starts-with";
    public const string EndsWith = "ends-with";
    public const string Contains = "contains";
    public const string Regex = "regex";

    public static readonly IReadOnlyList<string> All = [Extension, Glob, StartsWith, EndsWith, Contains, Regex];

    /// <summary>A pathological pattern gives up instead of hanging a scan; a timed-out test does not match.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Why <paramref name="pattern"/> cannot be used with <paramref name="matcher"/>, or null when it can.</summary>
    public static string? Validate(string matcher, string? pattern)
    {
        if (!All.Contains(matcher)) return $"Unknown matcher '{matcher}'.";
        if (string.IsNullOrWhiteSpace(pattern)) return "The pattern is empty.";
        if (matcher == Extension && ParseExtensions(pattern).Count == 0) return "No extension in the pattern.";
        if (matcher != Regex) return null;

        try
        {
            _ = new RegexEngine(pattern, RegexOptions.None, RegexTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Builds the test <c>(name, path) → match</c>. Throws for a pattern <see cref="Validate"/> rejects.</summary>
    public static Func<string, string, bool> Create(string matcher, string pattern, bool caseSensitive)
    {
        if (Validate(matcher, pattern) is { } error) throw new ArgumentException(error, nameof(pattern));

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var options = RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

        switch (matcher)
        {
            case Extension:
                var extensions = ParseExtensions(pattern);
                // Catalog extensions are lower-case, so a case-sensitive "MKV" matches nothing — as asked.
                return (name, _) => CatalogPath.Extension(name) is { } ext
                    && (caseSensitive ? extensions.Contains(ext) : extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
            case StartsWith:
                return (name, _) => name.StartsWith(pattern, comparison);
            case EndsWith:
                return (name, _) => name.EndsWith(pattern, comparison);
            case Contains:
                return (name, _) => name.Contains(pattern, comparison);
            case Glob when pattern.Contains('/'):
                var pathGlob = new RegexEngine(GlobToRegex(pattern.TrimStart('/')), options, RegexTimeout);
                return (_, path) => Test(pathGlob, path.TrimStart('/'));
            case Glob:
                var nameGlob = new RegexEngine(GlobToRegex(pattern), options, RegexTimeout);
                return (name, _) => Test(nameGlob, name);
            default:
                var regex = new RegexEngine(pattern, options, RegexTimeout);
                return (name, _) => Test(regex, name);
        }
    }

    private static bool Test(RegexEngine regex, string input)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static HashSet<string> ParseExtensions(string pattern) =>
        pattern.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Select(e => e.TrimStart('.'))
               .Where(e => e.Length > 0)
               .ToHashSet(StringComparer.Ordinal);

    /// <summary><c>*</c> stays within a segment, <c>**</c> crosses them, <c>?</c> is one character.</summary>
    private static string GlobToRegex(string glob)
    {
        var sb = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                i++;
                if (i + 1 < glob.Length && glob[i + 1] == '/')
                {
                    i++;
                    sb.Append("(?:.*/)?");
                }
                else sb.Append(".*");
            }
            else if (c == '*') sb.Append("[^/]*");
            else if (c == '?') sb.Append("[^/]");
            else sb.Append(RegexEngine.Escape(c.ToString()));
        }
        return sb.Append('$').ToString();
    }
}
