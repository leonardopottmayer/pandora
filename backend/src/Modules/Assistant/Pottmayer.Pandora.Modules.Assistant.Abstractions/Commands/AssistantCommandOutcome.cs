namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// The result of running a command handler. <see cref="Message"/> is a short, user-facing sentence in
/// the user's language: a confirmation of what happened on success, or the reason it did not on failure.
/// The pipeline records it on the invocation and echoes it back — it never claims a success the handler
/// did not report.
/// </summary>
public sealed record AssistantCommandOutcome(bool Success, string Message)
{
    public static AssistantCommandOutcome Ok(string message) => new(true, message);

    public static AssistantCommandOutcome Failed(string message) => new(false, message);
}
