using System.Globalization;
using System.Text;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;

/// <summary>
/// A file the user shared with the assistant bot, already downloaded: an image or a PDF. <see cref="Caption"/>
/// is what they wrote with it; <see cref="Words"/> is the same, lowercase and without accents, which is what
/// queues match their keywords against ("Finanças" → <c>financas</c>).
/// </summary>
public sealed record SharedFile(Guid UserId, string FileName, string ContentType, byte[] Content, string Caption)
{
    /// <summary>The largest file a bot can download from Telegram.</summary>
    public const long MaxBytes = 20 * 1024 * 1024;

    public IReadOnlySet<string> Words { get; } = WordsOf(Caption);

    /// <summary>Only images and PDFs are kept.</summary>
    public static bool IsSupported(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>The words of a caption, lowercase and without accents.</summary>
    public static IReadOnlySet<string> WordsOf(string text)
    {
        var plain = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                plain.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return new HashSet<string>(plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
