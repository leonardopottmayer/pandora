using Pottmayer.Pandora.Modules.Agenda.Application.Tasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class TaskAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);

    // ── Alert ──

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(60)]
    public void FiringInstant_offsets_the_anchor(int offset)
    {
        var alert = Alert.Create(Guid.NewGuid(), AlertSubjectType.Task, Guid.NewGuid(), offset, null, Time);
        Assert.Equal(Now.AddMinutes(offset), alert.FiringInstant(Now));
    }

    [Fact]
    public void An_empty_channel_array_is_stored_as_null()
    {
        var alert = Alert.Create(Guid.NewGuid(), AlertSubjectType.Task, Guid.NewGuid(), 0, [], Time);
        Assert.Null(alert.Channels);
    }

    [Fact]
    public void CopyFor_repoints_the_alert_at_a_new_subject_keeping_its_settings()
    {
        var original = Alert.Create(Guid.NewGuid(), AlertSubjectType.Task, Guid.NewGuid(), -30, ["telegram"], Time);
        var newSubject = Guid.NewGuid();

        var copy = original.CopyFor(newSubject, Time);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(newSubject, copy.SubjectId);
        Assert.Equal(AlertSubjectType.Task, copy.SubjectType);
        Assert.Equal(-30, copy.OffsetMinutes);
        Assert.Equal(["telegram"], copy.Channels!);
        Assert.True(copy.IsEnabled);
    }

    // ── TaskButtonPayload ──

    [Fact]
    public void Task_payload_round_trips_to_the_task_id()
    {
        var taskId = Guid.NewGuid();
        Assert.True(TaskButtonPayload.TryParse(TaskButtonPayload.For(taskId), out var parsed));
        Assert.Equal(taskId, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("d7c3f6e2-0000-0000-0000-000000000000")]       // a bare reminder id
    [InlineData("d7c3f6e2-0000-0000-0000-000000000000|123")]   // a reminder occurrence
    [InlineData("task|not-a-guid")]
    public void A_non_task_payload_is_ignored(string? payload)
        => Assert.False(TaskButtonPayload.TryParse(payload, out _));
}
