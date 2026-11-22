using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IEffectiveTimeZoneResolver"/> for the quiet-hours path. A null zone resolves to
/// UTC (the resolver's own last resort), so tests that do not care about the zone read as before.
/// </summary>
internal sealed class FakeEffectiveTimeZoneResolver(string? timeZone) : IEffectiveTimeZoneResolver
{
    private readonly TimeZoneInfo _zone =
        timeZone is null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZone);

    public Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default)
        => Task.FromResult(_zone);

    public TimeZoneInfo ResolveDefault() => _zone;
}
