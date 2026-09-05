using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>Returns a fixed preferences snapshot (or none), so the pipeline's reference clock is deterministic.</summary>
internal sealed class FakeUserPreferencesReader(UserPreferencesSnapshot? snapshot) : IUserPreferencesReader
{
    public static FakeUserPreferencesReader With(string timeZone) =>
        new(new UserPreferencesSnapshot(timeZone, DayOfWeek.Monday, -15));

    public static FakeUserPreferencesReader None() => new((UserPreferencesSnapshot?)null);

    public Task<UserPreferencesSnapshot?> GetAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(snapshot);
}
