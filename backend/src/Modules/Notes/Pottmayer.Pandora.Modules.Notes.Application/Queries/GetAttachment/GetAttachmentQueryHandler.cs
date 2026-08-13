using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetAttachment;

public sealed class GetAttachmentQueryHandler(IUnitOfWorkFactory factory, IFileStorage fileStorage)
    : QueryHandlerBase<GetAttachmentQuery, AttachmentContentDto>
{
    protected override async Task<Result<AttachmentContentDto>> HandleAsync(
        GetAttachmentQuery request, CancellationToken ct)
    {
        var attachment = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
            await ctx.AcquireRepository<IAttachmentRepository>()
                     .GetByIdAsync(request.Input.AttachmentId, token),
            cancellationToken: ct);

        if (attachment is null)
            return Fail(AttachmentErrors.NotFound);

        var blob = await fileStorage.GetAsync(attachment.StorageKey, ct);
        if (blob is null)
            return Fail(AttachmentErrors.NotFound);

        // The attachment row is the authoritative metadata; the blob only supplies the bytes.
        return Ok(new AttachmentContentDto(attachment.FileName, attachment.ContentType, blob.Content));
    }
}
