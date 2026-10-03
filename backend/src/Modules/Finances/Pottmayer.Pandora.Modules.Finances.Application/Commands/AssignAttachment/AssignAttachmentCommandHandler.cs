using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AssignAttachment;

public sealed class AssignAttachmentCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<AssignAttachmentCommand, AttachmentDto>
{
    protected override async Task<Result<AttachmentDto>> HandleAsync(AssignAttachmentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (Attachment.OwnerCount(input.TransactionId, input.PendingTransactionId, input.CardStatementId) != 1)
            return Fail(AttachmentErrors.OwnerRequired);

        return await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAttachmentRepository>();
            var attachment = await repo.FindByIdForUserAsync(input.AttachmentId, input.UserId, token);
            if (attachment is null)
                return Fail(AttachmentErrors.NotFound);
            if (!attachment.IsQueued)
                return Fail(AttachmentErrors.AlreadyAssigned);
            if (!await AttachmentOwners.ExistsAsync(ctx, input.UserId, input.TransactionId, input.PendingTransactionId, input.CardStatementId, token))
                return Fail(AttachmentErrors.OwnerNotFound);

            attachment.AssignTo(input.TransactionId, input.PendingTransactionId, input.CardStatementId);
            await repo.UpdateAsync(attachment, token);
            await AttachmentOwners.RecordAsync(ctx, attachment, added: true, timeProvider.GetUtcNow(), token);
            return Ok(AttachmentDto.From(attachment));
        }, cancellationToken: ct);
    }
}
