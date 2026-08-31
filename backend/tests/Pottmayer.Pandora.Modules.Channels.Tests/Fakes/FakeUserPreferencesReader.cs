using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IUserPreferencesReader"/> returning a fixed snapshot (or null). Only the
/// time zone matters to the quiet-hours path.
/// </summary>
internal sealed class FakeUserPreferencesReader(string? timeZone) : IUserPreferencesReader
{
    public Task<UserPreferencesSnapshot?> GetAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(timeZone is null ? null : new UserPreferencesSnapshot(timeZone, DayOfWeek.Sunday, 0));
}
