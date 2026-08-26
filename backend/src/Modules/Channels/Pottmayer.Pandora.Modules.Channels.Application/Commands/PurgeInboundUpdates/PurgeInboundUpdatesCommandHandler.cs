using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.PurgeInboundUpdates;

/// <summary>
/// Clears aged-out raw inbound payloads (<c>chn004.raw</c>). Mirrors the refresh-token purge: a job
/// drives this command on an interval, and the rows are kept — only their raw JSON is nulled.
/// </summary>
public sealed class PurgeInboundUpdatesCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<PurgeInboundUpdatesCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(
        PurgeInboundUpdatesCommand request, CancellationToken ct)
    {
        var purged = await factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var repo = context.AcquireRepository<IInboundUpdateRepository>();
            return await repo.PurgeRawOlderThanAsync(request.Input.RawOlderThan, token);
        }, cancellationToken: ct);

        return Ok(purged);
    }
}
