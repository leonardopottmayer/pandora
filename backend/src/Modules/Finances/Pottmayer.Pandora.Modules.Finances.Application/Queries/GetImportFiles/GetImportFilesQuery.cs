using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFiles;

public sealed record GetImportFilesInput(Guid UserId, ImportFileFilter Filter);

public sealed class GetImportFilesQuery(GetImportFilesInput input)
    : QueryBase<GetImportFilesInput, IReadOnlyList<ImportFileDto>>(input);
