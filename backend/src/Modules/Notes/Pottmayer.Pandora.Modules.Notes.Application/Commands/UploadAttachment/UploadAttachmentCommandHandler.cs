using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UploadAttachment;

public sealed class UploadAttachmentCommandHandler(
    IUnitOfWorkFactory factory,
    IFileStorage fileStorage,
    TimeProvider timeProvider)
    : CommandHandlerBase<UploadAttachmentCommand, AttachmentDto>
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    protected override async Task<Result<AttachmentDto>> HandleAsync(
        UploadAttachmentCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Content.Length == 0)
            return Fail(AttachmentErrors.Empty);
        if (input.Content.Length > MaxFileSizeBytes)
            return Fail(AttachmentErrors.TooLarge);

        // A pinned attachment must hang off a page the user actually owns (404-on-foreign-resource rule).
        if (input.PageId is { } pageId)
        {
            var pageExists = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
                await ctx.AcquireRepository<IPageRepository>()
                         .FindByIdForUserAsync(pageId, input.UserId, token) is not null,
                cancellationToken: ct);

            if (!pageExists)
                return Fail(AttachmentErrors.PageNotFound);
        }

        // Store the bytes first (like object storage: the blob write and its metadata row commit
        // separately), then record the attachment that points at them.
        var storageKey = await fileStorage.SaveAsync(input.FileName, input.ContentType, input.Content, ct);

        var attachment = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAttachmentRepository>();
            var entity = Attachment.Create(
                input.PageId, input.FileName, input.ContentType, input.Content.Length,
                fileStorage.Backend, storageKey, timeProvider);
            await repo.AddAsync(entity, token);
            return entity;
        }, cancellationToken: ct);

        return Ok(AttachmentDto.From(attachment));
    }
}
