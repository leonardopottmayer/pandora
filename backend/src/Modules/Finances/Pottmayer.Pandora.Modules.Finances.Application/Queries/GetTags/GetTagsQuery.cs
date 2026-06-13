using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTags;

public sealed record GetTagsInput(Guid UserId);

public sealed class GetTagsQuery(GetTagsInput input)
    : QueryBase<GetTagsInput, IReadOnlyList<TagDto>>(input);
