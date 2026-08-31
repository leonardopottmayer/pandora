using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

/// <summary>
/// What happens to a notification that lands inside the user's quiet hours. Deliberately narrow:
/// <c>defer_to_end</c> is not offered, because holding a delivery until morning is scheduling, and
/// scheduling does not live in Channels — a suppressed notification is simply not sent.
/// </summary>
public sealed class QuietHoursBehaviour : IDomainValue<QuietHoursBehaviour>
{
    /// <summary>Drop the delivery entirely. The event still happened; the notification just never goes out.</summary>
    public static readonly QuietHoursBehaviour Suppress = new("suppress");

    /// <summary>Ignore the quiet window and deliver as usual. Lets a user keep a window that only mutes some sources later.</summary>
    public static readonly QuietHoursBehaviour DeliverAnyway = new("deliver_anyway");

    public string Value { get; }

    private QuietHoursBehaviour(string value) => Value = value;

    public static QuietHoursBehaviour FromValue(string value) => value switch
    {
        "suppress" => Suppress,
        "deliver_anyway" => DeliverAnyway,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown quiet-hours behaviour.")
    };

    /// <summary>Parses without throwing, for values that arrive from outside (a request body).</summary>
    public static bool TryFromValue(string? value, out QuietHoursBehaviour behaviour)
    {
        switch (value?.ToLowerInvariant())
        {
            case "suppress":
                behaviour = Suppress;
                return true;
            case "deliver_anyway":
                behaviour = DeliverAnyway;
                return true;
            default:
                behaviour = Suppress;
                return false;
        }
    }

    public override string ToString() => Value;
}
