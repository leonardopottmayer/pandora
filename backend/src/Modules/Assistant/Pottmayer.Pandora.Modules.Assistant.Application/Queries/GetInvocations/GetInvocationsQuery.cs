using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetInvocations;

public sealed record GetInvocationsInput(Guid UserId, int Limit);

[RequiresPermission(AssistantPermissions.Commands.Read)]
public sealed class GetInvocationsQuery(GetInvocationsInput input)
    : QueryBase<GetInvocationsInput, IReadOnlyList<InvocationDto>>(input);
