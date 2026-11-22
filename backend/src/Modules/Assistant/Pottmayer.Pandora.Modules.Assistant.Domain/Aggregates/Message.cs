using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;

/// <summary>
/// One turn stored in a <see cref="Conversation"/>: the user's utterance, or the assistant's reply. Kept
/// for context and for the audit trail — the model's structured tool call lives on the
/// <see cref="CommandInvocation"/>, not here.
/// </summary>
public sealed class Message : AggregateRoot<Guid>
{
    public Guid ConversationId { get; private set; }
    public MessageAuthor Author { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Message() { }

    public static Message Create(Guid conversationId, MessageAuthor author, string content, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Author = author,
            Content = content,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
