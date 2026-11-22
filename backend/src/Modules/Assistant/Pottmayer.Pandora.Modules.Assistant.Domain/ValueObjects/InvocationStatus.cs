using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;

/// <summary>
/// How one interpretation ended. The audit trail records exactly one of these, so the outcome of every
/// utterance is inspectable after the fact — including the ones that did nothing.
/// </summary>
public sealed class InvocationStatus : IDomainValue<InvocationStatus>
{
    /// <summary>A tool call was produced, validated and the command ran successfully.</summary>
    public static readonly InvocationStatus Executed = new("executed");

    /// <summary>The command ran but its use case rejected the request (a business failure).</summary>
    public static readonly InvocationStatus Failed = new("failed");

    /// <summary>The model asked a clarifying question instead of calling a tool — nothing ran.</summary>
    public static readonly InvocationStatus Clarification = new("clarification");

    /// <summary>The model named an unknown tool or produced arguments that failed validation.</summary>
    public static readonly InvocationStatus Rejected = new("rejected");

    /// <summary>The provider could not be reached or refused the call — nothing ran.</summary>
    public static readonly InvocationStatus ProviderError = new("provider-error");

    /// <summary>A valid tool call the policy requires the user to confirm before it runs. Awaits confirm/cancel.</summary>
    public static readonly InvocationStatus PendingConfirmation = new("pending-confirmation");

    /// <summary>A pending confirmation the user declined.</summary>
    public static readonly InvocationStatus Cancelled = new("cancelled");

    /// <summary>A pending confirmation that timed out before the user answered.</summary>
    public static readonly InvocationStatus Expired = new("expired");

    public string Value { get; }

    private InvocationStatus(string value) => Value = value;

    public static InvocationStatus FromValue(string value) => value switch
    {
        "executed" => Executed,
        "failed" => Failed,
        "clarification" => Clarification,
        "rejected" => Rejected,
        "provider-error" => ProviderError,
        "pending-confirmation" => PendingConfirmation,
        "cancelled" => Cancelled,
        "expired" => Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown invocation status.")
    };

    public override string ToString() => Value;
}
