namespace Pottmayer.Pandora.Modules.Notifications.Infrastructure;

/// <summary>
/// Configuration for the Notifications module (bound from the <c>Notifications</c> section).
/// </summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>URL template for activation links; <c>{token}</c> is replaced at render time.</summary>
    public string ActivationUrlTemplate { get; set; } = "https://localhost/activate?token={token}";

    /// <summary>How often the dispatcher worker drains the queue.</summary>
    public int DispatchIntervalSeconds { get; set; } = 15;

    /// <summary>How many notifications the worker processes per tick.</summary>
    public int DispatchBatchSize { get; set; } = 20;
}
