using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetSpaces;

public sealed record GetSpacesInput(Guid UserId, bool IncludeArchived);

/// <summary>The user's spaces, the default first, each with its page count and last edit.</summary>
[RequiresPermission(NotesPermissions.Spaces.Read)]
public sealed class GetSpacesQuery(GetSpacesInput input)
    : QueryBase<GetSpacesInput, IReadOnlyList<SpaceDto>>(input);
