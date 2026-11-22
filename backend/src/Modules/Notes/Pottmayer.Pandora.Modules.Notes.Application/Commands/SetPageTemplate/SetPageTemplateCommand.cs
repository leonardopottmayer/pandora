using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageTemplate;

public sealed record SetPageTemplateInput(Guid UserId, Guid PageId, bool IsTemplate);

/// <summary>Makes an owned page available as a template, or removes that designation.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetPageTemplateCommand(SetPageTemplateInput input) : CommandBase<SetPageTemplateInput, PageDto>(input);
