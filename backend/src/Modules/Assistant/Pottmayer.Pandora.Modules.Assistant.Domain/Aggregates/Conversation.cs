using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;

/// <summary>
/// A short-lived thread of interpretations for one user. It groups the messages and invocations that
/// belong together so a follow-up ("yes", "and tomorrow too") has context. A conversation lapses after
/// 30 minutes of silence — past that, the next utterance opens a fresh one.
/// </summary>
public sealed class Conversation : AggregateRoot<Guid>
{
    /// <summary>Silence after which a conversation is considered over.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public Guid UserId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }

    private Conversation() { }

    public static Conversation Start(Guid userId, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            StartedAt = now,
            LastActivityAt = now
        };
    }

    /// <summary>True when the conversation has been silent past <see cref="IdleTimeout"/> at <paramref name="now"/>.</summary>
    public bool IsExpired(DateTimeOffset now) => now - LastActivityAt > IdleTimeout;

    /// <summary>Records activity, keeping the conversation alive.</summary>
    public void Touch(DateTimeOffset now) => LastActivityAt = now;
}
