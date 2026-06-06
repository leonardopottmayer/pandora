namespace Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;

/// <summary>A <see cref="TimeProvider"/> pinned to a fixed instant, so backoff math is deterministic.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
