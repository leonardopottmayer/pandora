using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;

public sealed record GetPageInput(Guid UserId, Guid PageId);

[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetPageQuery(GetPageInput input)
    : QueryBase<GetPageInput, PageDto>(input);
