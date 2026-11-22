namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>One row of the assistant's audit trail, as shown in the invocation log.</summary>
public sealed record InvocationDto(
    Guid Id,
    Guid ConversationId,
    string Utterance,
    string? CommandName,
    string? Arguments,
    string Status,
    string? Result,
    string? Error,
    string Provider,
    string Model,
    long LatencyMs,
    int PromptTokens,
    int CompletionTokens,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
