using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFile;

public sealed record GetImportFileInput(Guid UserId, Guid ImportFileId);

public sealed class GetImportFileQuery(GetImportFileInput input)
    : QueryBase<GetImportFileInput, ImportFileDto>(input);
