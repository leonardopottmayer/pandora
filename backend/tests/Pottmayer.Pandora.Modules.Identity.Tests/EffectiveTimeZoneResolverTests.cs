using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Preferences;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class EffectiveTimeZoneResolverTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static EffectiveTimeZoneResolver Build(string? preference, string @default = "America/Sao_Paulo")
        => new(new StubReader(preference), Options.Create(new TimeZoneOptions { DefaultTimeZone = @default }));

    [Fact]
    public async Task Prefers_the_stored_preference()
    {
        var zone = await Build(preference: "America/New_York").ResolveAsync(User);
        Assert.Equal("America/New_York", zone.Id);
    }

    [Fact]
    public async Task Falls_back_to_the_configured_default_when_there_is_no_preference()
    {
        // The Telegram case: a user who never had a preferences row must still land on a real zone,
        // not UTC — that is the bug this whole change fixes.
        var zone = await Build(preference: null).ResolveAsync(User);
        Assert.Equal("America/Sao_Paulo", zone.Id);
    }

    [Fact]
    public async Task An_explicit_request_wins_over_preference_and_default()
    {
        var zone = await Build(preference: "America/New_York").ResolveAsync(User, requested: "Europe/Lisbon");
        Assert.Equal("Europe/Lisbon", zone.Id);
    }

    [Fact]
    public async Task An_unknown_requested_zone_is_ignored_and_the_chain_continues()
    {
        var zone = await Build(preference: "America/New_York").ResolveAsync(User, requested: "Mars/Olympus");
        Assert.Equal("America/New_York", zone.Id);
    }

    [Fact]
    public async Task Falls_back_to_utc_when_neither_preference_nor_default_names_a_real_zone()
    {
        var zone = await Build(preference: null, @default: "Nowhere/Nothing").ResolveAsync(User);
        Assert.Equal(TimeZoneInfo.Utc.Id, zone.Id);
    }

    [Fact]
    public void ResolveDefault_returns_the_configured_zone_with_no_user()
    {
        var zone = Build(preference: null).ResolveDefault();
        Assert.Equal("America/Sao_Paulo", zone.Id);
    }

    private sealed class StubReader(string? timeZone) : IUserPreferencesReader
    {
        public Task<UserPreferencesSnapshot?> GetAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(timeZone is null ? null : new UserPreferencesSnapshot(timeZone, DayOfWeek.Monday, -15));
    }
}
