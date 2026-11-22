using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;

/// <summary>
/// One append-only row in the <c>int003</c> integration event log. It is history, not state: rows are
/// written and read, never mutated, so the timeline of a connection's health survives even the
/// deletion of the account it refers to (a disconnect removes <c>int001</c> but keeps its log).
/// </summary>
public sealed class IntegrationEventLogEntry : AggregateRoot<Guid>
{
    private const int MaxDetailLength = 1000;

    public Guid UserId { get; private set; }

    /// <summary>The account this concerns. Kept even after the account is deleted; no FK, by design.</summary>
    public Guid? ExternalAccountId { get; private set; }

    public string Provider { get; private set; } = null!;
    public IntegrationEventType EventType { get; private set; } = null!;

    /// <summary>The failure reason on a failure kind, otherwise null.</summary>
    public string? Detail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private IntegrationEventLogEntry() { }

    public static IntegrationEventLogEntry Record(
        Guid userId,
        Guid? externalAccountId,
        string provider,
        IntegrationEventType eventType,
        string? detail,
        TimeProvider timeProvider) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ExternalAccountId = externalAccountId,
            Provider = provider,
            EventType = eventType,
            Detail = detail is null ? null : Truncate(detail),
            OccurredAt = timeProvider.GetUtcNow()
        };

    private static string Truncate(string value) =>
        value.Length <= MaxDetailLength ? value : value[..MaxDetailLength];
}
