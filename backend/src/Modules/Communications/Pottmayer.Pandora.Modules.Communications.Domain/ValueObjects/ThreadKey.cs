namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>
/// How an IMAP message finds its conversation: the first Message-ID of <c>References</c> is the root of
/// the thread, so every reply carries it; a message that starts a thread uses its own Message-ID.
/// Gmail uses its own <c>threadId</c> instead.
/// </summary>
public static class ThreadKey
{
    /// <param name="fallback">Used when the message has no Message-ID at all (the provider message id).</param>
    public static string From(string? messageId, IEnumerable<string>? references, string fallback) =>
        Normalize(references?.FirstOrDefault(r => Normalize(r) is not null))
        ?? Normalize(messageId)
        ?? fallback;

    /// <summary>Without the angle brackets, so <c>&lt;a@b&gt;</c> and <c>a@b</c> are one thread.</summary>
    private static string? Normalize(string? id)
    {
        var trimmed = id?.Trim().Trim('<', '>').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
