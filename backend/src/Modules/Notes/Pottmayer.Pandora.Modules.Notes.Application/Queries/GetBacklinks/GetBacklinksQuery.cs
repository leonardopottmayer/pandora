using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetBacklinks;

public sealed record GetBacklinksInput(Guid UserId, Guid PageId);

[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetBacklinksQuery(GetBacklinksInput input)
    : QueryBase<GetBacklinksInput, IReadOnlyList<BacklinkDto>>(input);
