using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachmentContent;

public sealed class GetAttachmentContentQueryHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(FinancesModule.DatabaseKey)] IFileStorage fileStorage)
    : QueryHandlerBase<GetAttachmentContentQuery, AttachmentContentDto>
{
    protected override async Task<Result<AttachmentContentDto>> HandleAsync(
        GetAttachmentContentQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var attachment = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
            await ctx.AcquireRepository<IAttachmentRepository>().FindByIdForUserAsync(input.AttachmentId, input.UserId, token),
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
