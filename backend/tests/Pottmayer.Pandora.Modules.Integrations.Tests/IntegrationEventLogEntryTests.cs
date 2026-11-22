using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Integrations.Tests;

public sealed class IntegrationEventLogEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTime(Now);

    [Fact]
    public void Record_stamps_the_occurrence_time_and_fields()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var entry = IntegrationEventLogEntry.Record(
            userId, accountId, "google", IntegrationEventType.Revoked, "invalid_grant", Clock);

        Assert.Equal(userId, entry.UserId);
        Assert.Equal(accountId, entry.ExternalAccountId);
        Assert.Equal("google", entry.Provider);
        Assert.Equal(IntegrationEventType.Revoked, entry.EventType);
        Assert.Equal("invalid_grant", entry.Detail);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public void Record_truncates_an_over_long_detail()
    {
        var entry = IntegrationEventLogEntry.Record(
            Guid.NewGuid(), null, "google", IntegrationEventType.RefreshFailed, new string('x', 5000), Clock);

        Assert.Equal(1000, entry.Detail!.Length);
    }

    [Fact]
    public void Record_keeps_a_null_detail_null()
    {
        var entry = IntegrationEventLogEntry.Record(
            Guid.NewGuid(), Guid.NewGuid(), "google", IntegrationEventType.Connected, null, Clock);

        Assert.Null(entry.Detail);
    }

    [Theory]
    [InlineData("connected")]
    [InlineData("reconnected")]
    [InlineData("refresh-failed")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("disconnected")]
    public void EventType_round_trips_through_its_value(string value)
    {
        Assert.Equal(value, IntegrationEventType.FromValue(value).Value);
    }

    [Fact]
    public void EventType_rejects_an_unknown_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationEventType.FromValue("nope"));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
