using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class NoteAiErrors
{
    public static Error InvalidPrompt => Error.Validation("Notes.InvalidPrompt", "Enter a prompt of up to 8000 characters.");

    public static Error InvalidQuestion => Error.Validation("Notes.InvalidQuestion", "Enter a question of up to 1000 characters.");
    public static Error InvalidAnswer => Error.Validation("Notes.InvalidAnswer", "The AI did not return an answer with valid note sources. Try rephrasing your question.");

    public static Error InvalidPageSummary => Error.Validation("Notes.InvalidPageSummary",
        "A page summary needs between 1 and 32000 characters of text. Summarize a selection for larger pages.");

    public static Error InvalidTransform => Error.Validation("Notes.InvalidTransform",
        "Choose summarize, improve, translate, or continue; provide up to 8000 characters of text and context, and a target language for translation.");

    public static Error InvalidSource => Error.Validation("Notes.InvalidSource", "The edit source must be edit or assistant.");
}
