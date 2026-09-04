using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProfile;

public sealed record GetProfileInput(Guid UserId);

public sealed class GetProfileQuery(GetProfileInput input)
    : QueryBase<GetProfileInput, AssistantProfileDto>(input);
