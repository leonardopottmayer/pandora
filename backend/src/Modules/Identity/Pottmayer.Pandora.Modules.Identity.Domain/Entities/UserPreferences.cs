using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

public sealed class UserPreferences : Entity<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public AppTheme Theme { get; private set; } = null!;
    public AppLanguage Language { get; private set; } = null!;

    /// <summary>IANA time zone (e.g. "America/Sao_Paulo"). The reference clock for the Agenda.</summary>
    public string TimeZone { get; private set; } = null!;

    /// <summary>First day of the week, for calendar rendering.</summary>
    public DayOfWeek WeekStartsOn { get; private set; }

    /// <summary>Default signed offset, in minutes, for alerts on events and tasks (e.g. -15 = fifteen minutes before).</summary>
    public int DefaultAlertOffsetMinutes { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private UserPreferences() { }

    internal static UserPreferences Create(
        AppTheme theme, AppLanguage language,
        string timeZone, DayOfWeek weekStartsOn, int defaultAlertOffsetMinutes) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Theme = theme,
            Language = language,
            TimeZone = timeZone,
            WeekStartsOn = weekStartsOn,
            DefaultAlertOffsetMinutes = defaultAlertOffsetMinutes,
        };

    public void Update(
        AppTheme theme, AppLanguage language,
        string timeZone, DayOfWeek weekStartsOn, int defaultAlertOffsetMinutes)
    {
        Theme = theme;
        Language = language;
        TimeZone = timeZone;
        WeekStartsOn = weekStartsOn;
        DefaultAlertOffsetMinutes = defaultAlertOffsetMinutes;
    }
}
