using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
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
        if (input.TransactionId.HasValue == input.PendingTransactionId.HasValue)
            return Fail(AttachmentErrors.OwnerRequired);

        var attachments = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
            await ctx.AcquireRepository<IAttachmentRepository>()
                .GetByOwnerAsync(input.UserId, input.TransactionId, input.PendingTransactionId, token),
            cancellationToken: ct);

        return Ok((IReadOnlyList<AttachmentDto>)[.. attachments.Select(AttachmentDto.From)]);
    }
}
