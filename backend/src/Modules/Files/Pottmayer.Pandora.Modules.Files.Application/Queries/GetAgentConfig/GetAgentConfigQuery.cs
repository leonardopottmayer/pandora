using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetAgentConfig;

public sealed record GetAgentConfigInput(Guid UserId, Guid DeviceId);

[RequiresPermission(FilesPermissions.Roots.Read)]
public sealed class GetAgentConfigQuery(GetAgentConfigInput input)
    : QueryBase<GetAgentConfigInput, AgentConfig>(input);
