using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetUserChannels;

public sealed record GetUserChannelsInput(Guid UserId);

public sealed class GetUserChannelsQuery(GetUserChannelsInput input)
    : QueryBase<GetUserChannelsInput, IReadOnlyList<UserChannelDto>>(input);
