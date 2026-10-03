using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachments;

public sealed class GetAttachmentsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    protected override async Task<Result<IReadOnlyList<AttachmentDto>>> HandleAsync(
        GetAttachmentsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        // Exactly one of the three owners, or the queue.
        if (Attachment.OwnerCount(input.TransactionId, input.PendingTransactionId, input.CardStatementId) + (input.Queued ? 1 : 0) != 1)
            return Fail(AttachmentErrors.OwnerRequired);

        var attachments = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
            await ctx.AcquireRepository<IAttachmentRepository>()
                .GetByOwnerAsync(input.UserId, input.TransactionId, input.PendingTransactionId, input.CardStatementId, token),
            cancellationToken: ct);

        return Ok((IReadOnlyList<AttachmentDto>)[.. attachments.Select(AttachmentDto.From)]);
    }
}
