using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadAttachment;

public sealed record UploadAttachmentInput(
    Guid UserId,
    Guid? TransactionId,
    Guid? PendingTransactionId,
    Guid? CardStatementId,
    string Kind,
    string FileName,
    string ContentType,
    byte[] Content,
    string? Note = null);

/// <summary>
/// Stores an image or PDF and attaches it to one of the user's transactions, pending transactions or card
/// statements (at most one of the ids) — or, with none, parks it in the queue to be filed later.
/// </summary>
public sealed class UploadAttachmentCommand(UploadAttachmentInput input)
    : CommandBase<UploadAttachmentInput, AttachmentDto>(input);
