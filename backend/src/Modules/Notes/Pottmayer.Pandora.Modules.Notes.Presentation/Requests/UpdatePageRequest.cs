using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record UpdatePageRequest(
    string Title,
    string? Icon,
    JsonElement? Content,
    string Source = "edit");
