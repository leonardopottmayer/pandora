using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AssignAttachment;

public sealed record AssignAttachmentInput(
    Guid UserId, Guid AttachmentId, Guid? TransactionId, Guid? PendingTransactionId, Guid? CardStatementId);

/// <summary>
/// Files a queued attachment (one shared with the assistant bot) under exactly one of the user's
/// transactions, pending transactions or card statements.
/// </summary>
[RequiresPermission(FinancesPermissions.Attachments.Update)]
public sealed class AssignAttachmentCommand(AssignAttachmentInput input)
    : CommandBase<AssignAttachmentInput, AttachmentDto>(input);
