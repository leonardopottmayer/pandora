using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadAttachment;

public sealed record UploadAttachmentInput(
    Guid UserId,
    Guid? TransactionId,
    Guid? PendingTransactionId,
    string Kind,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>
/// Stores an image or PDF and attaches it to one of the user's transactions or pending transactions
/// (exactly one of the two ids).
/// </summary>
public sealed class UploadAttachmentCommand(UploadAttachmentInput input)
    : CommandBase<UploadAttachmentInput, AttachmentDto>(input);
