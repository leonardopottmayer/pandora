using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class CalendarTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);
    private static readonly Guid User = Guid.NewGuid();

    private static Calendar Make(string name = "Personal", bool isDefault = false, string zone = "UTC") =>
        Calendar.Create(User, name, "#3366ff", isDefault, zone, CalendarOrigin.Local, Time);

    [Fact]
    public void Create_starts_visible_and_local()
    {
        var cal = Make(isDefault: true);
        Assert.True(cal.IsVisible);
        Assert.True(cal.IsDefault);
        Assert.Equal(CalendarOrigin.Local, cal.Origin);
        Assert.Equal("#3366ff", cal.Color);
        Assert.Null(cal.ArchivedAt);
    }

    [Fact]
    public void Create_requires_a_name()
        => Assert.Throws<ArgumentException>(() => Make(name: "  "));

    [Fact]
    public void Create_rejects_an_unknown_zone()
        => Assert.Throws<ArgumentException>(() => Make(zone: "Mars/Olympus"));

    [Fact]
    public void Update_changes_the_editable_fields()
    {
        var cal = Make();
        cal.Update("Work", "#ff0000", isVisible: false, "America/New_York");

        Assert.Equal("Work", cal.Name);
        Assert.Equal("#ff0000", cal.Color);
        Assert.False(cal.IsVisible);
        Assert.Equal("America/New_York", cal.TimeZone);
    }

    [Fact]
    public void Archive_is_idempotent()
    {
        var cal = Make();
        cal.Archive(Time);
        var first = cal.ArchivedAt;
        cal.Archive(new FixedTimeProvider(Now.AddHours(1)));
        Assert.Equal(first, cal.ArchivedAt);
    }
}
