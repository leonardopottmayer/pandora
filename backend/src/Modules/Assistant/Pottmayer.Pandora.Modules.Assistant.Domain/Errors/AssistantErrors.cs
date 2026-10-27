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
        Error.Validation("Assistant.NoApiKey", $"No '{provider}' key is selected for the assistant, or it was disconnected. Pick one in the assistant settings.");

    public static Error ChatAccountNotFound =>
        Error.Validation("Assistant.ChatAccountNotFound", "The selected account was not found among your connections.");

    public static Error ChatAccountMismatch(string provider) =>
        Error.Validation("Assistant.ChatAccountMismatch", $"The selected account is not a '{provider}' API key.");

    public static Error EmptyText =>
        Error.Validation("Assistant.EmptyText", "Say something for the assistant to interpret.");

    public static Error InvocationNotFound =>
        Error.NotFound("Assistant.InvocationNotFound", "That interpretation was not found.");

    public static Error NotPendingConfirmation =>
        Error.Validation("Assistant.NotPendingConfirmation", "That interpretation is not awaiting confirmation.");

    public static Error ConfirmationExpired =>
        Error.Validation("Assistant.ConfirmationExpired", "This confirmation has expired. Ask again.");
}
