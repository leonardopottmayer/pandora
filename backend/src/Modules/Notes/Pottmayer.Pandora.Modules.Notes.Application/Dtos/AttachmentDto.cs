using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// Metadata returned after an upload. <see cref="Url"/> is the authenticated download path the client
/// embeds into markdown (e.g. <c>![](/api/v1/notes/attachments/{id})</c>).
/// </summary>
public sealed record AttachmentDto(
    Guid Id,
    Guid? PageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Url,
    DateTimeOffset CreatedAt)
{
    public static AttachmentDto From(Attachment a) =>
        new(a.Id, a.PageId, a.FileName, a.ContentType, a.SizeBytes,
            $"/api/v1/notes/attachments/{a.Id}", a.CreatedAt);
}
