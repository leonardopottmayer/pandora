using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;

public sealed record ConfirmInvocationInput(Guid UserId, Guid InvocationId);

/// <summary>Runs a tool call that was held for confirmation, if it is still pending and unexpired.</summary>
[RequiresPermission(AssistantPermissions.Commands.Run)]
public sealed class ConfirmInvocationCommand(ConfirmInvocationInput input)
    : CommandBase<ConfirmInvocationInput, InvocationResultDto>(input);
