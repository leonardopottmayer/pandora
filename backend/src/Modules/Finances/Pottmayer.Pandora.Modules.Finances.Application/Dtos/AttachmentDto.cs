using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>An attachment's metadata. <see cref="Url"/> is the authenticated path that serves its bytes.</summary>
public sealed record AttachmentDto(
    Guid Id,
    Guid? TransactionId,
    Guid? PendingTransactionId,
    string Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Url,
    DateTimeOffset CreatedAt)
{
    public static AttachmentDto From(Attachment a) =>
        new(a.Id, a.TransactionId, a.PendingTransactionId, a.Kind.Value, a.FileName, a.ContentType, a.SizeBytes,
            $"/api/v1/finances/attachments/{a.Id}", a.CreatedAt);
}

/// <summary>The bytes and headers needed to serve an attachment download.</summary>
public sealed record AttachmentContentDto(string FileName, string ContentType, byte[] Content);
