using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAttachment;

public sealed record DeleteAttachmentInput(Guid UserId, Guid AttachmentId);

/// <summary>Removes an attachment and its bytes.</summary>
public sealed class DeleteAttachmentCommand(DeleteAttachmentInput input)
    : CommandBase<DeleteAttachmentInput, bool>(input);
