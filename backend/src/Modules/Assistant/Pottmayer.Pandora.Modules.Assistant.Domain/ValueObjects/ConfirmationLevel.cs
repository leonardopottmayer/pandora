using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;

/// <summary>
/// How readily the assistant executes a write without asking first. It shifts every command's own
/// <c>ConfirmationPolicy</c> one notch: <see cref="Strict"/> confirms more, <see cref="Trusting"/>
/// confirms less. <see cref="Balanced"/> leaves each command's policy as declared.
/// </summary>
public sealed class ConfirmationLevel : IDomainValue<ConfirmationLevel>
{
    /// <summary>Confirm more than the command declares — the cautious setting.</summary>
    public static readonly ConfirmationLevel Strict = new("strict");

    /// <summary>Use each command's declared policy unchanged. The default.</summary>
    public static readonly ConfirmationLevel Balanced = new("balanced");

    /// <summary>Confirm less than the command declares — execute unambiguous intents more freely.</summary>
    public static readonly ConfirmationLevel Trusting = new("trusting");

    public string Value { get; }

    private ConfirmationLevel(string value) => Value = value;

    public static ConfirmationLevel FromValue(string value) => value switch
    {
        "strict" => Strict,
        "balanced" => Balanced,
        "trusting" => Trusting,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown confirmation level.")
    };

    public override string ToString() => Value;
}
