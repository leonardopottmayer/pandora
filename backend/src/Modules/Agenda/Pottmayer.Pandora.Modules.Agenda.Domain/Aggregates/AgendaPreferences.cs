using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>What a user prefers about the Agenda itself. No row means the defaults.</summary>
public sealed class AgendaPreferences : IAuditable
{
    public const int DefaultAlertOffset = -15;

    public Guid UserId { get; private set; }

    /// <summary>Signed minutes, relative to the item (e.g. -15 = fifteen minutes before).</summary>
    public int DefaultAlertOffsetMinutes { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private AgendaPreferences() { }

    public static AgendaPreferences Create(Guid userId, int defaultAlertOffsetMinutes) =>
        new() { UserId = userId, DefaultAlertOffsetMinutes = defaultAlertOffsetMinutes };

    public void Update(int defaultAlertOffsetMinutes) => DefaultAlertOffsetMinutes = defaultAlertOffsetMinutes;
}
