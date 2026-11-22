using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetUserChannels;

public sealed record GetUserChannelsInput(Guid UserId);

[RequiresPermission(ChannelsPermissions.Channels.Read)]
public sealed class GetUserChannelsQuery(GetUserChannelsInput input)
    : QueryBase<GetUserChannelsInput, IReadOnlyList<UserChannelDto>>(input);
