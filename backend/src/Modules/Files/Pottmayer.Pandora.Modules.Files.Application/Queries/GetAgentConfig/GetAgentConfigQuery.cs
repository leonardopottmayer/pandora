using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetAgentConfig;

public sealed record GetAgentConfigInput(Guid UserId, Guid DeviceId);

public sealed class GetAgentConfigQuery(GetAgentConfigInput input)
    : QueryBase<GetAgentConfigInput, AgentConfig>(input);
