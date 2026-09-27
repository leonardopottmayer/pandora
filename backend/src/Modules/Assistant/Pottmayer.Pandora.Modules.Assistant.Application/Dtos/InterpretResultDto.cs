namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// What one interpretation produced, for the caller (command bar, Telegram) to render.
/// <see cref="Message"/> is the whole user-facing reply; <see cref="Invocations"/> holds one entry per
/// tool call the model made (a sentence can ask for several), or a single entry with no command when it
/// produced none (a clarification, a provider error). <see cref="ConversationId"/> lets the caller
/// continue the same thread. <see cref="Transcript"/> is what the assistant heard when the input was a
/// voice note (null for text), so the caller can echo it back.
/// </summary>
public sealed record InterpretResultDto(
    Guid ConversationId,
    string Message,
    IReadOnlyList<InvocationResultDto> Invocations,
    string? Transcript = null);

/// <summary>
/// One recorded invocation. <see cref="Status"/> mirrors the invocation status; <see cref="CommandName"/>
/// and <see cref="Arguments"/> expose the exact tool call (null when there was none); <see cref="Message"/>
/// is this invocation's part of the reply. When <see cref="Status"/> is <c>pending-confirmation</c>,
/// <see cref="InvocationId"/> is what the caller posts to confirm/cancel.
/// </summary>
public sealed record InvocationResultDto(
    Guid InvocationId,
    string Status,
    string? CommandName,
    string? Arguments,
    string Message);
