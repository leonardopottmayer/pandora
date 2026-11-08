namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record TransformTextRequest(string Action, string Text, string? Language = null, string? Context = null);
