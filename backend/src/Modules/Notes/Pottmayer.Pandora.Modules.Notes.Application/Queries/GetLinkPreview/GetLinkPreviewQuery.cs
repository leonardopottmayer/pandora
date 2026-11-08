using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetLinkPreview;

public sealed record GetLinkPreviewInput(Guid UserId, string Url);

/// <summary>A web page's title, image and embed, for a bookmark or an embed block of a page.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetLinkPreviewQuery(GetLinkPreviewInput input)
    : QueryBase<GetLinkPreviewInput, LinkPreview>(input);
