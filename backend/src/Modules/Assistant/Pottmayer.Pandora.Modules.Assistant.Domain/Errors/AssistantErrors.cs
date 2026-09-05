using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Errors;

public static class AssistantErrors
{
    public static Error ModelRequired =>
        Error.Validation("Assistant.ModelRequired", "A chat model is required.");

    public static Error UnknownConfirmationLevel(string value) =>
        Error.Validation("Assistant.UnknownConfirmationLevel", $"Confirmation level '{value}' is not valid.");

    public static Error NotEnabled =>
        Error.Validation("Assistant.NotEnabled", "The assistant is not enabled. Turn it on in settings first.");

    public static Error NoApiKey(string provider) =>
        Error.Validation("Assistant.NoApiKey", $"No API key configured for '{provider}'. Add it under Integrations.");

    public static Error EmptyText =>
        Error.Validation("Assistant.EmptyText", "Say something for the assistant to interpret.");

    public static Error InvocationNotFound =>
        Error.NotFound("Assistant.InvocationNotFound", "That interpretation was not found.");

    public static Error NotPendingConfirmation =>
        Error.Validation("Assistant.NotPendingConfirmation", "That interpretation is not awaiting confirmation.");

    public static Error ConfirmationExpired =>
        Error.Validation("Assistant.ConfirmationExpired", "This confirmation has expired. Ask again.");
}
