using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;

public sealed record CancelInvocationInput(Guid UserId, Guid InvocationId);

/// <summary>Declines a tool call that was held for confirmation, leaving it cancelled and unrun.</summary>
public sealed class CancelInvocationCommand(CancelInvocationInput input)
    : CommandBase<CancelInvocationInput, InterpretResultDto>(input);
