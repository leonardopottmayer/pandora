using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;

/// <param name="Audio">
/// A voice note to interpret instead of <paramref name="Text"/>: it is transcribed first, and the
/// transcript then runs through the same pipeline as typed text.
/// </param>
public sealed record InterpretInput(Guid UserId, string? Text, Guid? ConversationId = null, ChatAttachment? Audio = null);

/// <summary>
/// Interpret one sentence (typed, or transcribed from a voice note) and, when it maps cleanly to a command, execute it. A command (not a query):
/// it makes a real, billed provider call and can change state.
/// </summary>
public sealed class InterpretCommand(InterpretInput input)
    : CommandBase<InterpretInput, InterpretResultDto>(input);
