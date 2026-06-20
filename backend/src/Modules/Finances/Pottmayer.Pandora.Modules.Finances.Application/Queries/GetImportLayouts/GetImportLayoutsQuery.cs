using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportLayouts;

public sealed record GetImportLayoutsInput(Guid UserId);

public sealed class GetImportLayoutsQuery(GetImportLayoutsInput input)
    : QueryBase<GetImportLayoutsInput, IReadOnlyList<ImportLayoutDto>>(input);
