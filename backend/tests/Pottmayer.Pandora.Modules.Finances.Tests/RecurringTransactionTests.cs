using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class RecurringTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private static RecurringTransaction NewMonthly(DateOnly? endDate = null, int? maxOccurrences = null, bool autoPost = false) =>
        RecurringTransaction.Create(
            userId: UserId,
            name: "Rent",
            accountId: AccountId,
            cardId: null,
            kind: "expense",
            amount: 1500m,
            amountIsEstimate: false,
            description: "Monthly rent",
            payee: null,
            systemCategoryId: null,
            userCategoryId: null,
            frequency: "monthly",
            interval: 1,
            dayOfMonth: null,
            weekday: null,
            startDate: Start,
            endDate: endDate,
            maxOccurrences: maxOccurrences,
            autoPost: autoPost,
            autoGenerate: true,
            timeProvider: new FixedTimeProvider(Now));

    [Fact]
    public void Create_stamps_initial_state()
    {
        var r = NewMonthly();

        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal("Rent", r.Name);
        Assert.True(r.IsActive);
        Assert.Equal(Start, r.NextOccurrenceOn);
        Assert.Equal(0, r.OccurrencesCount);
        Assert.Equal(Now, r.CreatedAt);
    }

    [Fact]
    public void Pause_sets_status_to_paused()
    {
        var r = NewMonthly();
        var changed = r.Pause();
        Assert.True(changed);
        Assert.True(r.IsPaused);
    }

    [Fact]
    public void Pause_returns_false_when_already_paused()
    {
        var r = NewMonthly();
        r.Pause();
        Assert.False(r.Pause());
        Assert.True(r.IsPaused);
    }

    [Fact]
    public void Resume_restores_active_status()
    {
        var r = NewMonthly();
        r.Pause();
        var changed = r.Resume();
        Assert.True(changed);
        Assert.True(r.IsActive);
    }

    [Fact]
    public void Resume_returns_false_when_active()
    {
        var r = NewMonthly();
        Assert.False(r.Resume());
        Assert.True(r.IsActive);
    }

    [Fact]
    public void AdvanceCursor_increments_count_and_moves_next_occurrence()
    {
        var r = NewMonthly();
        r.AdvanceCursor(Start);

        Assert.Equal(1, r.OccurrencesCount);
        Assert.Equal(new DateOnly(2026, 7, 1), r.NextOccurrenceOn);
        Assert.True(r.IsActive);
    }

    [Fact]
    public void AdvanceCursor_finishes_when_maxOccurrences_reached()
    {
        var r = NewMonthly(maxOccurrences: 1);
        r.AdvanceCursor(Start);

        Assert.Equal(1, r.OccurrencesCount);
        Assert.True(r.IsFinished);
    }

    [Fact]
    public void AdvanceCursor_finishes_when_next_exceeds_endDate()
    {
        var endDate = new DateOnly(2026, 6, 30);
        var r = NewMonthly(endDate: endDate);
        r.AdvanceCursor(Start);

        // Next would be 2026-07-01 which is after endDate → finished
        Assert.True(r.IsFinished);
    }

    [Fact]
    public void AdvanceCursor_stays_active_when_next_within_endDate()
    {
        var endDate = new DateOnly(2026, 7, 31);
        var r = NewMonthly(endDate: endDate);
        r.AdvanceCursor(Start);

        // Next is 2026-07-01 which is before endDate → still active
        Assert.True(r.IsActive);
    }

    [Fact]
    public void UpdateTemplate_trims_name_and_description()
    {
        var r = NewMonthly();
        r.UpdateTemplate("  Updated  ", 2000m, false, "  New desc  ", null, null, null, null, null, false, true);

        Assert.Equal("Updated", r.Name);
        Assert.Equal(2000m, r.Amount);
    }

    [Fact]
    public void UpdateTemplate_does_not_change_frequency_or_destination()
    {
        var r = NewMonthly();
        r.UpdateTemplate("Name", 100m, false, "Desc", null, null, null, null, null, false, true);

        Assert.Equal("monthly", r.Frequency);
        Assert.Equal(AccountId, r.AccountId);
        Assert.Null(r.CardId);
    }

    [Fact]
    public void GetRule_returns_rule_matching_stored_fields()
    {
        var r = NewMonthly();
        var rule = r.GetRule();

        Assert.Equal("monthly", rule.Frequency);
        Assert.Equal(1, rule.Interval);
        Assert.Equal(Start, rule.StartDate);
    }
}
