using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SummarizePage;

public sealed record SummarizePageInput(Guid UserId, Guid PageId);

/// <summary>Generates a preview of a saved page's summary without changing the page.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SummarizePageCommand(SummarizePageInput input) : CommandBase<SummarizePageInput, TextTransformDto>(input);
