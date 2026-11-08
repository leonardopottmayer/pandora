using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.GenerateText;

public sealed record GenerateTextInput(Guid UserId, string Prompt);

/// <summary>Generates a preview for an AI block; accepting and saving it is a separate action.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class GenerateTextCommand(GenerateTextInput input) : CommandBase<GenerateTextInput, AiGenerationDto>(input);
