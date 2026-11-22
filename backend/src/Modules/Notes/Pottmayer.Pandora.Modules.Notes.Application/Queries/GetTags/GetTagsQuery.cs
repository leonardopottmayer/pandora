using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetTags;

public sealed record GetTagsInput(Guid UserId);

/// <summary>The user's tags with how many live pages carry each — what the filters list.</summary>
[RequiresPermission(NotesPermissions.Tags.Read)]
public sealed class GetTagsQuery(GetTagsInput input)
    : QueryBase<GetTagsInput, IReadOnlyList<TagDto>>(input);
