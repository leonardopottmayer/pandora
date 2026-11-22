using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolderProperties;

public sealed record GetFolderPropertiesInput(Guid UserId, Guid FolderId, bool IncludeArchived = false, IReadOnlyCollection<Guid>? TagIds = null);

/// <summary>Reads the properties of a folder's direct pages without returning or changing their content.</summary>
[RequiresPermission(NotesPermissions.Folders.Read)]
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetFolderPropertiesQuery(GetFolderPropertiesInput input)
    : QueryBase<GetFolderPropertiesInput, FolderPropertiesDto>(input);
