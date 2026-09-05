using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;

/// <summary>
/// One row of the assistant's audit trail: what the user said, the tool call the model produced, and how
/// it ended. Most rows are terminal the moment they are written (executed, a clarifying question, a
/// rejected argument, an unreachable provider). One kind is not: a <see cref="InvocationStatus.PendingConfirmation"/>
/// row is a valid tool call the policy held back — it later transitions to executed/failed (confirm),
/// cancelled, or expired. It also carries the provider/model/latency/token cost of the call.
/// </summary>
public sealed class CommandInvocation : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }

    /// <summary>The conversation this interpretation belongs to.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>The user's raw sentence, as typed.</summary>
    public string Utterance { get; private set; } = null!;

    /// <summary>The tool the model chose, or null when it produced no tool call.</summary>
    public string? CommandName { get; private set; }

    /// <summary>The tool-call arguments as raw JSON, or null when there was no tool call.</summary>
    public string? ArgumentsJson { get; private set; }

    public InvocationStatus Status { get; private set; } = null!;

    /// <summary>The user-facing outcome message on success, or the model's clarifying question.</summary>
    public string? Result { get; private set; }

    /// <summary>The failure reason when the command or the call did not succeed.</summary>
    public string? Error { get; private set; }

    public string Provider { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public long LatencyMs { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }

    /// <summary>When a pending confirmation stops being answerable. Null for terminal rows.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private CommandInvocation() { }

    public static CommandInvocation Create(
        Guid userId,
        Guid conversationId,
        string utterance,
        string? commandName,
        string? argumentsJson,
        InvocationStatus status,
        string? result,
        string? error,
        string provider,
        string model,
        long latencyMs,
        int promptTokens,
        int completionTokens,
        DateTimeOffset? expiresAt,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ConversationId = conversationId,
            Utterance = utterance,
            CommandName = commandName,
            ArgumentsJson = argumentsJson,
            Status = status,
            Result = result,
            Error = error,
            Provider = provider,
            Model = model,
            LatencyMs = latencyMs,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            ExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>True when this row is a confirmation still waiting for the user at <paramref name="now"/>.</summary>
    public bool IsAwaitingConfirmation(DateTimeOffset now) =>
        Status == InvocationStatus.PendingConfirmation && (ExpiresAt is null || ExpiresAt > now);

    /// <summary>The pending confirmation ran and succeeded.</summary>
    public void MarkExecuted(string result)
    {
        Status = InvocationStatus.Executed;
        Result = result;
        Error = null;
        ExpiresAt = null;
    }

    /// <summary>The pending confirmation ran but the command rejected it.</summary>
    public void MarkFailed(string error)
    {
        Status = InvocationStatus.Failed;
        Result = null;
        Error = error;
        ExpiresAt = null;
    }

    /// <summary>The user declined the pending confirmation.</summary>
    public void Cancel()
    {
        Status = InvocationStatus.Cancelled;
        ExpiresAt = null;
    }

    /// <summary>The pending confirmation timed out before the user answered.</summary>
    public void MarkExpired()
    {
        Status = InvocationStatus.Expired;
        ExpiresAt = null;
    }
}
