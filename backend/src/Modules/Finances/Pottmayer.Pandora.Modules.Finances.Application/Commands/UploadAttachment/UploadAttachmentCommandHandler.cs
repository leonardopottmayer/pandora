using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadAttachment;

public sealed class UploadAttachmentCommandHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(FinancesModule.DatabaseKey)] IFileStorage fileStorage,
    TimeProvider timeProvider)
    : CommandHandlerBase<UploadAttachmentCommand, AttachmentDto>
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB, as in Notes

    protected override async Task<Result<AttachmentDto>> HandleAsync(
        UploadAttachmentCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var owners = Attachment.OwnerCount(input.TransactionId, input.PendingTransactionId, input.CardStatementId);
        if (owners > 1)
            return Fail(AttachmentErrors.OwnerRequired);
        if (!AttachmentKind.IsSupported(input.Kind))
            return Fail(AttachmentErrors.InvalidKind(input.Kind));
        if (input.Content.Length == 0)
            return Fail(AttachmentErrors.Empty);
        if (input.Content.Length > MaxFileSizeBytes)
            return Fail(AttachmentErrors.TooLarge);
        if (!IsImageOrPdf(input.ContentType))
            return Fail(AttachmentErrors.UnsupportedType);

        if (owners == 1 && !await factory.ExecuteAsync(FinancesModule.DatabaseKey, (ctx, token) =>
                AttachmentOwners.ExistsAsync(ctx, input.UserId, input.TransactionId, input.PendingTransactionId, input.CardStatementId, token),
                cancellationToken: ct))
            return Fail(AttachmentErrors.OwnerNotFound);

        // Store the bytes first (like object storage: the blob write and its metadata row commit
        // separately), then record the attachment that points at them.
        var storageKey = await fileStorage.SaveAsync(input.FileName, input.ContentType, input.Content, ct);

        var attachment = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var entity = Attachment.Create(
                input.UserId, input.TransactionId, input.PendingTransactionId, input.CardStatementId,
                AttachmentKind.FromValue(input.Kind), input.FileName, input.ContentType, input.Content.Length,
                fileStorage.Backend, storageKey, input.Note, timeProvider);
            await ctx.AcquireRepository<IAttachmentRepository>().AddAsync(entity, token);
            await AttachmentOwners.RecordAsync(ctx, entity, added: true, entity.CreatedAt, token);
            return entity;
        }, cancellationToken: ct);

        return Ok(AttachmentDto.From(attachment));
    }

    private static bool IsImageOrPdf(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
}
