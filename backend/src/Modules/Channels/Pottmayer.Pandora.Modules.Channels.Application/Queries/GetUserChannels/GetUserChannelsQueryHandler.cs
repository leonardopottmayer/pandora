using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetUserChannels;

public sealed class GetUserChannelsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetUserChannelsQuery, IReadOnlyList<UserChannelDto>>
{
    protected override async Task<Result<IReadOnlyList<UserChannelDto>>> HandleAsync(
        GetUserChannelsQuery request, CancellationToken cancellationToken)
    {
        var channels = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IUserChannelRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<UserChannelDto> dtos = [.. channels.Select(c => new UserChannelDto(
            c.Channel.Value,
            c.Address.Value,
            c.IsVerified,
            c.IsEnabled,
            c.DisabledReason,
            c.VerifiedAt))];

        return Ok(dtos);
    }
}
