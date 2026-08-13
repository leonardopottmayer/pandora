using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UploadAttachment;

public sealed record UploadAttachmentInput(
    Guid UserId,
    Guid? PageId,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>
/// Stores an uploaded file and records an attachment for it. When <c>PageId</c> is set, the page must
/// be one the user owns; a bare upload (no page) is allowed so the client can embed it afterwards.
/// </summary>
public sealed class UploadAttachmentCommand(UploadAttachmentInput input)
    : CommandBase<UploadAttachmentInput, AttachmentDto>(input);
