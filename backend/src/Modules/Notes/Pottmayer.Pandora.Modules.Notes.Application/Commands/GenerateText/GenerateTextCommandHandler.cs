using Pottmayer.Pandora.Modules.Assistant.Abstractions.Text;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.GenerateText;

/// <summary>Sends only the block's prompt to the user's configured Assistant, without tools or history.</summary>
public sealed class GenerateTextCommandHandler(ITextCompletion completion, TimeProvider timeProvider)
    : CommandHandlerBase<GenerateTextCommand, AiGenerationDto>
{
    protected override async Task<Result<AiGenerationDto>> HandleAsync(GenerateTextCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Prompt) || input.Prompt.Length > 8000)
            return Fail(NoteAiErrors.InvalidPrompt);
        var result = await completion.CompleteAsync(input.UserId,
            "Fulfill the user's writing prompt. Return only plain text, without HTML, markdown fences or commentary. " +
            "Use the prompt's language unless it requests another language. Do not call tools or claim to have accessed the user's notes or performed actions.",
            input.Prompt.Trim(), ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(new AiGenerationDto(result.Value!, timeProvider.GetUtcNow()));
    }
}
