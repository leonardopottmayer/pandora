using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>Resolves to a fixed zone, so the pipeline's reference clock is deterministic.</summary>
internal sealed class FakeEffectiveTimeZoneResolver(TimeZoneInfo zone) : IEffectiveTimeZoneResolver
{
    public static FakeEffectiveTimeZoneResolver With(string iana) =>
        new(TimeZoneInfo.FindSystemTimeZoneById(iana));

    public Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default)
        => Task.FromResult(zone);

    public TimeZoneInfo ResolveDefault() => zone;
}
