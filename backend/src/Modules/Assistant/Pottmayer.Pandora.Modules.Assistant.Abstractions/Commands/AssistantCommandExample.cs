namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// One few-shot example for a command: a natural utterance and the arguments the model should produce
/// for it (as a raw JSON object). The system prompt renders these, in the user's language, so the model
/// learns the shape of each tool from real sentences.
/// </summary>
public sealed record AssistantCommandExample(string Utterance, string ArgumentsJson);
