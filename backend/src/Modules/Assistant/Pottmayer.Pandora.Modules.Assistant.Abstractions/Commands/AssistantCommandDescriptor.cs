namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// What the assistant advertises to the model for one command: its <see cref="Name"/> (the tool name),
/// a <see cref="Description"/> the model reads to decide when to use it, the
/// <see cref="ParametersJsonSchema"/> describing its arguments, its <see cref="Confirmation"/> policy,
/// and few-shot <see cref="Examples"/>. A module that owns a command contributes one of these alongside
/// its <see cref="IAssistantTool"/>; the assistant renders it into a Tars
/// <c>ToolDefinition</c>.
/// </summary>
public sealed record AssistantCommandDescriptor(
    string Name,
    string Description,
    string ParametersJsonSchema,
    ConfirmationPolicy Confirmation,
    IReadOnlyList<AssistantCommandExample> Examples);
