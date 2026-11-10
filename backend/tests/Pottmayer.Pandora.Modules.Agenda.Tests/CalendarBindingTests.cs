using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class CalendarBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static CalendarBinding NewBinding() =>
        CalendarBinding.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "primary", new FixedTimeProvider(Now));

    [Fact]
    public void A_new_binding_lists_everything_then_only_changes_until_a_month_passes()
    {
        var binding = NewBinding();
        Assert.True(binding.NeedsFullSync(Now, TimeSpan.FromDays(30)));

        binding.RecordSuccess("token-1", wasFullSync: true, Now);
        Assert.False(binding.NeedsFullSync(Now.AddDays(29), TimeSpan.FromDays(30)));
        Assert.True(binding.NeedsFullSync(Now.AddDays(30), TimeSpan.FromDays(30)));

        binding.ResetCursor();
        Assert.True(binding.NeedsFullSync(Now, TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Five_failures_in_a_row_stop_the_binding_and_sync_now_resumes_it()
    {
        var binding = NewBinding();
        for (var i = 0; i < CalendarBinding.MaxConsecutiveFailures - 1; i++)
            binding.RecordFailure("Google Calendar answered 500.");
        Assert.Equal(CalendarBindingStatus.Ok, binding.Status);

        binding.RecordFailure("Google Calendar answered 500.");
        Assert.Equal(CalendarBindingStatus.Error, binding.Status);

        binding.Resume();
        Assert.Equal(CalendarBindingStatus.Ok, binding.Status);
        Assert.Equal(0, binding.FailureCount);
    }

    [Fact]
    public void A_success_clears_the_last_error_and_a_lost_account_waits_for_the_user()
    {
        var binding = NewBinding();
        binding.MarkNeedsReconnect("revoked");
        Assert.Equal(CalendarBindingStatus.NeedsReconnect, binding.Status);

        binding.RecordSuccess("t", wasFullSync: false, Now);
        Assert.Equal(CalendarBindingStatus.Ok, binding.Status);
        Assert.Null(binding.LastError);
        Assert.Null(binding.FullSyncedAt);
    }
}
