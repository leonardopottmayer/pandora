using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// One update the bot received, written down before it is acted on. The provider's own update id is
/// unique here, which is what makes reprocessing harmless: the long-polling offset is confirmed by
/// writing this row, so a crash between the write and the processing replays instead of losing it.
/// </summary>
public sealed class InboundUpdate : AggregateRoot<Guid>
{
    public string Provider { get; private set; } = null!;
    public long ProviderUpdateId { get; private set; }
    public string Raw { get; private set; } = "{}";
    public Guid? UserId { get; private set; }
    public InboundClassification Classification { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private InboundUpdate() { }

    public static InboundUpdate Record(
        string provider,
        long providerUpdateId,
        string raw,
        Guid? userId,
        InboundClassification classification,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Provider = provider,
            ProviderUpdateId = providerUpdateId,
            Raw = raw,
            UserId = userId,
            Classification = classification,
            ReceivedAt = timeProvider.GetUtcNow()
        };

    public void MarkProcessed(TimeProvider timeProvider) => ProcessedAt = timeProvider.GetUtcNow();
}
