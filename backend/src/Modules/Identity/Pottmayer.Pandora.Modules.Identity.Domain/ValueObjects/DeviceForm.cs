using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

/// <summary>What kind of client a paired device is: an app with a window, a background agent, or a phone.</summary>
public sealed record DeviceForm : IDomainValue<DeviceForm>
{
    public string Value { get; }

    private DeviceForm(string value) => Value = value;

    public static readonly DeviceForm Desktop  = new("desktop");
    public static readonly DeviceForm Headless = new("headless");
    public static readonly DeviceForm Mobile   = new("mobile");

    public static readonly IReadOnlyList<DeviceForm> All = [Desktop, Headless, Mobile];

    public static bool IsSupported(string? value) => All.Any(f => f.Value == value);

    public static DeviceForm FromValue(string value) =>
        All.FirstOrDefault(f => f.Value == value)
        ?? throw new ArgumentException($"Unsupported device form value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
