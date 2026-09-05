using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;

public sealed record ConfirmInvocationInput(Guid UserId, Guid InvocationId);

/// <summary>Runs a tool call that was held for confirmation, if it is still pending and unexpired.</summary>
public sealed class ConfirmInvocationCommand(ConfirmInvocationInput input)
    : CommandBase<ConfirmInvocationInput, InterpretResultDto>(input);
