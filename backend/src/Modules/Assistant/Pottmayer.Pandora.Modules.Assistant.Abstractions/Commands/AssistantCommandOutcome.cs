namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// The result of running a command handler. <see cref="Message"/> is a short, user-facing sentence in
/// the user's language: a confirmation of what happened on success, or the reason it did not on failure.
/// The pipeline records it on the invocation and echoes it back — it never claims a success the handler
/// did not report.
/// <para>
/// <see cref="Recap"/> is what the conversation history keeps of this reply — the history is re-sent to
/// the model on the next turn. A tool that reads the user's data (their agenda, say) sets it to a
/// content-free line, so the data reaches the user and never the hosted model. Null keeps
/// <see cref="Message"/>.
/// </para>
/// </summary>
public sealed record AssistantCommandOutcome(bool Success, string Message, string? Recap = null)
{
    public static AssistantCommandOutcome Ok(string message) => new(true, message);

    /// <summary>A success whose <paramref name="message"/> carries the user's data; the history keeps <paramref name="recap"/>.</summary>
    public static AssistantCommandOutcome Ok(string message, string recap) => new(true, message, recap);

    public static AssistantCommandOutcome Failed(string message) => new(false, message);
}
