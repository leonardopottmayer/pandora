using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Text;

/// <summary>Completes text using the user's enabled Assistant profile, without tools or conversation history.</summary>
public interface ITextCompletion
{
    Task<Result<string>> CompleteAsync(Guid userId, string instruction, string text, CancellationToken ct = default);
}
