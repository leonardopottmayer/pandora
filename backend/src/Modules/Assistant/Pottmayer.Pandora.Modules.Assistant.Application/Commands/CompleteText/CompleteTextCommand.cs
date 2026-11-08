using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.CompleteText;

public sealed record CompleteTextInput(Guid UserId, string Instruction, string Text);

/// <summary>Generates text for another module using the user's Assistant settings and permissions.</summary>
[RequiresPermission(AssistantPermissions.Commands.Run)]
public sealed class CompleteTextCommand(CompleteTextInput input) : CommandBase<CompleteTextInput, string>(input);
