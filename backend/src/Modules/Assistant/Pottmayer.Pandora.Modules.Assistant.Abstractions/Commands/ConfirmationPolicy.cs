namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// How readily a command executes once the model has produced a tool call. The user's profile
/// <c>ConfirmationLevel</c> shifts this one notch either way before the pipeline acts on it.
/// </summary>
public enum ConfirmationPolicy
{
    /// <summary>Execute straight away — the intent is a low-stakes write.</summary>
    Never,

    /// <summary>Execute when the tool call is unambiguous; otherwise confirm first.</summary>
    WhenAmbiguous,

    /// <summary>Always confirm before executing.</summary>
    Always,
}
