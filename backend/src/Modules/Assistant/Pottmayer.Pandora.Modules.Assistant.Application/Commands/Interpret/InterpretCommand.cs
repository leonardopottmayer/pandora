using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;

public sealed record InterpretInput(Guid UserId, string Text, Guid? ConversationId = null);

/// <summary>
/// Interpret one sentence and, when it maps cleanly to a command, execute it. A command (not a query):
/// it makes a real, billed provider call and can change state.
/// </summary>
public sealed class InterpretCommand(InterpretInput input)
    : CommandBase<InterpretInput, InterpretResultDto>(input);
