using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAttachment;

public sealed class DeleteAttachmentCommandHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(FinancesModule.DatabaseKey)] IFileStorage fileStorage,
    TimeProvider timeProvider)
    : CommandHandlerBase<DeleteAttachmentCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAttachmentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var storageKey = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAttachmentRepository>();
            var attachment = await repo.FindByIdForUserAsync(input.AttachmentId, input.UserId, token);
            if (attachment is null)
                return null;

            await repo.RemoveAsync(attachment, token);

            var (entityType, ownerId, eventType) = attachment.TransactionId is { } txId
                ? (TransactionEvents.EntityType, txId, TransactionEvents.AttachmentRemoved)
                : (PendingTransactionEvents.EntityType, attachment.PendingTransactionId!.Value, PendingTransactionEvents.AttachmentRemoved);
            await ctx.RecordAsync(input.UserId, input.UserId, entityType, ownerId, eventType, now,
                new { attachmentId = attachment.Id, kind = attachment.Kind.Value, fileName = attachment.FileName }, ct: token);
            return attachment.StorageKey;
        }, cancellationToken: ct);

        if (storageKey is null)
            return Fail(AttachmentErrors.NotFound);

        // The row goes first, so a failure here leaves an unreachable blob, never a row without bytes.
        await fileStorage.DeleteAsync(storageKey, ct);
        return Ok(true);
    }
}
