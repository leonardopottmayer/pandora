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
    /// Runs the tool for <see cref="AssistantToolContext.UserId"/> with the model-produced
    /// <paramref name="arguments"/> (already parsed from the tool call). Returns the outcome to record and
    /// echo back, in the context's locale; it must reflect the underlying use case's real result.
    /// </summary>
    Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default);

    /// <summary>
    /// The question shown when this call is held for confirmation — what would happen, in the user's
    /// words and zone (e.g. "Criar o lembrete \"Pagar o aluguel\" para 05/09/2026 às 10:00?"). Throws
    /// <see cref="ArgumentException"/> (or <see cref="FormatException"/>) for arguments it cannot read.
    /// </summary>
    string Describe(AssistantToolContext context, JsonElement arguments);
}
