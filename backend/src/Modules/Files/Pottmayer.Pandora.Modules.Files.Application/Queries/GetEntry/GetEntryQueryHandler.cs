using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetEntry;

public sealed class GetEntryQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetEntryQuery, EntryDto>
{
    protected override async Task<Result<EntryDto>> HandleAsync(GetEntryQuery request, CancellationToken cancellationToken)
    {
        var entry = await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IEntryRepository>().FindForUserAsync(request.Input.EntryId, request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return entry is null ? Fail(FilesErrors.EntryNotFound) : Ok(EntryDto.From(entry));
    }
}
