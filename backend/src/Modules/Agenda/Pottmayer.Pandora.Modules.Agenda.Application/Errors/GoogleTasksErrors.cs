using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

/// <summary>Google Tasks failures. A refused token and a generic failure reuse <see cref="GoogleCalendarErrors"/>.</summary>
public static class GoogleTasksErrors
{
    public const string MissingScopeCode = "Agenda.GoogleTasksMissingScope";

    /// <summary>The token was granted without the Tasks scope (an account connected before phase 6).</summary>
    public static Error MissingScope =>
        Error.Validation(MissingScopeCode, "The Google account was connected without access to its tasks. Reconnect it and allow Google Tasks.");

    /// <summary>Google's 403 SERVICE_DISABLED: the Tasks API is off in the OAuth client's Cloud project.</summary>
    public static Error ApiDisabled =>
        Error.Validation("Agenda.GoogleTasksApiDisabled",
            "The Google Tasks API is not enabled in the Google Cloud project of Pandora's OAuth client. Enable it in APIs & Services → Library, wait a few minutes and try again.");

    public static Error ListNotFound =>
        Error.NotFound("Agenda.GoogleTaskListNotFound", "This Google task list does not exist or the account cannot see it any more.");

    public static Error TaskNotFound =>
        Error.NotFound("Agenda.GoogleTaskNotFound", "This task no longer exists in Google Tasks.");

    public static Error BindingNotFound =>
        Error.NotFound("Agenda.TaskListBindingNotFound", "This Google task list link does not exist.");

    public static Error NothingToBind =>
        Error.Validation("Agenda.GoogleNothingToBind", "Pick at least one Google task list.");
}
