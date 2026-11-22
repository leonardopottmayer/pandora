using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolders;

public sealed record GetFoldersInput(Guid UserId, Guid? SpaceId);

/// <summary>The user's folders, of one space or all, flat: the frontend nests them.</summary>
[RequiresPermission(NotesPermissions.Folders.Read)]
public sealed class GetFoldersQuery(GetFoldersInput input)
    : QueryBase<GetFoldersInput, IReadOnlyList<FolderDto>>(input);
