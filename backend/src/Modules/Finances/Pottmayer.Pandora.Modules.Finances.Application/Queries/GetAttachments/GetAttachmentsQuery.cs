using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachments;

public sealed record GetAttachmentsInput(
    Guid UserId, Guid? TransactionId, Guid? PendingTransactionId, Guid? CardStatementId, bool Queued = false);

/// <summary>
/// The attachments of one transaction, pending transaction or card statement — or the queue — oldest first.
/// </summary>
public sealed class GetAttachmentsQuery(GetAttachmentsInput input)
    : QueryBase<GetAttachmentsInput, IReadOnlyList<AttachmentDto>>(input);
