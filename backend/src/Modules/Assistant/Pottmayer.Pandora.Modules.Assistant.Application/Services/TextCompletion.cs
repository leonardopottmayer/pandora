using Pottmayer.Pandora.Modules.Assistant.Abstractions.Text;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CompleteText;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Services;

/// <summary>Routes text completion through the Assistant's authorized command.</summary>
public sealed class TextCompletion(ISender sender) : ITextCompletion
{
    public async Task<Result<string>> CompleteAsync(Guid userId, string instruction, string text, CancellationToken ct = default)
        => await sender.Send(new CompleteTextCommand(new CompleteTextInput(userId, instruction, text)), ct);
}
