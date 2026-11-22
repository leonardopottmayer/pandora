namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>A <see cref="TimeProvider"/> whose wall clock is fixed. Timestamp/elapsed stay real (tiny).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
