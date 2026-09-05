using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// A tool a module contributes to the assistant catalog. The module registers one implementation per
/// tool; the assistant discovers them all through DI, renders each <see cref="Descriptor"/> as a tool
/// for the model, and calls <see cref="ExecuteAsync"/> with the validated arguments when the model picks
/// that tool. The tool is thin — it maps the arguments onto the module's existing use case (through the
/// mediator) and never duplicates its business rules.
/// </summary>
public interface IAssistantTool
{
    /// <summary>What the assistant advertises to the model for this tool.</summary>
    AssistantCommandDescriptor Descriptor { get; }

    /// <summary>
    /// Runs the tool for <paramref name="userId"/> with the model-produced <paramref name="arguments"/>
    /// (already parsed from the tool call). Returns the outcome to record and echo back; it must reflect
    /// the underlying use case's real result.
    /// </summary>
    Task<AssistantCommandOutcome> ExecuteAsync(Guid userId, JsonElement arguments, CancellationToken ct = default);
}
