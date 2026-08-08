using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;

public sealed record GetPageInput(Guid UserId, Guid PageId);

public sealed class GetPageQuery(GetPageInput input)
    : QueryBase<GetPageInput, PageDto>(input);
