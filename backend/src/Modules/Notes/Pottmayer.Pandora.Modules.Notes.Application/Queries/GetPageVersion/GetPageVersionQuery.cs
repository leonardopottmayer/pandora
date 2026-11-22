using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersion;

public sealed record GetPageVersionInput(Guid UserId, Guid PageId, long Number);

/// <summary>Reads one immutable version of a page owned by the user.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetPageVersionQuery(GetPageVersionInput input)
    : QueryBase<GetPageVersionInput, PageVersionDto>(input);
