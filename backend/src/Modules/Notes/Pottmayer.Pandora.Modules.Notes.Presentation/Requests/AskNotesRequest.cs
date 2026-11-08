namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record AskNotesRequest(string Question, Guid? SpaceId = null);
