using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.TransformText;

public sealed record TransformTextInput(Guid UserId, string Action, string Text, string? Language = null, string? Context = null);

/// <summary>Previews an AI rewrite or continuation; accepting it is a separate page save.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class TransformTextCommand(TransformTextInput input) : CommandBase<TransformTextInput, TextTransformDto>(input);
