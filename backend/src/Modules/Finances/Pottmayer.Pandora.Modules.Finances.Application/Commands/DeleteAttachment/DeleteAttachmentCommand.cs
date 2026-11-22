using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAttachment;

public sealed record DeleteAttachmentInput(Guid UserId, Guid AttachmentId);

/// <summary>Removes an attachment and its bytes.</summary>
[RequiresPermission(FinancesPermissions.Attachments.Delete)]
public sealed class DeleteAttachmentCommand(DeleteAttachmentInput input)
    : CommandBase<DeleteAttachmentInput, bool>(input);
