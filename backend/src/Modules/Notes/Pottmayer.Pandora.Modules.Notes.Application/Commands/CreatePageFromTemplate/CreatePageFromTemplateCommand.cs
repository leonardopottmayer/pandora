using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePageFromTemplate;

public sealed record CreatePageFromTemplateInput(Guid UserId, Guid TemplateId, string Title, Guid? SpaceId = null, Guid? FolderId = null);

/// <summary>Creates an independent page from an owned, active template and expands its text variables.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Pages.Create)]
public sealed class CreatePageFromTemplateCommand(CreatePageFromTemplateInput input)
    : CommandBase<CreatePageFromTemplateInput, PageDto>(input);
