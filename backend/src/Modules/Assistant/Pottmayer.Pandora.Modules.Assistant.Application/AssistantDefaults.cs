namespace Pottmayer.Pandora.Modules.Assistant.Application;

/// <summary>
/// Fallbacks used before a user has saved a profile, and for the reachability probe when no model is
/// supplied. The provider matches the Integrations account key and the AI client factory key.
/// </summary>
public static class AssistantDefaults
{
    public const string Provider = "gemini";

    /// <summary>A fast Gemini model. The user can change it in settings; the reachability test confirms it.</summary>
    public const string Model = "gemini-3.6-flash";
}
