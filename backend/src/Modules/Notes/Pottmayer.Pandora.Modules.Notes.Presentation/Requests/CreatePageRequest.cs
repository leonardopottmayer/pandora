using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record CreatePageRequest(
    string Title,
    Guid? ParentId,
    string? Icon,
    JsonElement? Content,
    Guid? SpaceId = null,
    Guid? FolderId = null);
