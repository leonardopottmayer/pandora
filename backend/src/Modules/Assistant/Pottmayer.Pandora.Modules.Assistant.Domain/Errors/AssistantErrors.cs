using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Errors;

public static class AssistantErrors
{
    public static Error ModelRequired =>
        Error.Validation("Assistant.ModelRequired", "A chat model is required.");

    public static Error UnknownConfirmationLevel(string value) =>
        Error.Validation("Assistant.UnknownConfirmationLevel", $"Confirmation level '{value}' is not valid.");
}
