using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;

public sealed record CancelInvocationInput(Guid UserId, Guid InvocationId);

/// <summary>Declines a tool call that was held for confirmation, leaving it cancelled and unrun.</summary>
[RequiresPermission(AssistantPermissions.Commands.Run)]
public sealed class CancelInvocationCommand(CancelInvocationInput input)
    : CommandBase<CancelInvocationInput, InvocationResultDto>(input);
