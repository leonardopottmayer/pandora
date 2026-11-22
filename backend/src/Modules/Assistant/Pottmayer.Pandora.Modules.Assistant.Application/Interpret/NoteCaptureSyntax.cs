namespace Pottmayer.Pandora.Modules.Assistant.Application.Interpret;

/// <summary>Recognizes the explicit text capture command, without interpreting its body.</summary>
public static class NoteCaptureSyntax
{
    public static bool TryParse(string? text, out string body)
    {
        body = string.Empty;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        var end = value.IndexOfAny([' ', '\t', '\r', '\n']);
        var command = end < 0 ? value : value[..end];
        var mention = command.IndexOf('@');
        if (mention >= 0) command = command[..mention];
        if (!command.Equals("/nota", StringComparison.OrdinalIgnoreCase)) return false;
        body = end < 0 ? string.Empty : value[(end + 1)..].Trim();
        return true;
    }
}
