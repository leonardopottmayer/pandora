namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// What one interpretation produced, for the caller (command bar) to render. <see cref="Status"/> mirrors
/// the recorded invocation status; <see cref="CommandName"/> and <see cref="Arguments"/> expose the exact
/// tool call the model made (null when it produced none); <see cref="Message"/> is the user-facing reply.
/// When <see cref="Status"/> is <c>pending-confirmation</c>, <see cref="InvocationId"/> is what the caller
/// posts to confirm/cancel. <see cref="ConversationId"/> lets the caller continue the same thread.
/// </summary>
public sealed record InterpretResultDto(
    Guid InvocationId,
    Guid ConversationId,
    string Status,
    string? CommandName,
    string? Arguments,
    string Message);
