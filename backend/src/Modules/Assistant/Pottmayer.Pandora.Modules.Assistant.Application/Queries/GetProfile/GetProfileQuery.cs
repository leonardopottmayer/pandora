using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProfile;

public sealed record GetProfileInput(Guid UserId);

[RequiresPermission(AssistantPermissions.Profile.Read)]
public sealed class GetProfileQuery(GetProfileInput input)
    : QueryBase<GetProfileInput, AssistantProfileDto>(input);
