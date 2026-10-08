using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.ExpireScans;

/// <summary>An agent that stopped sending batches (crashed, asleep, disconnected) leaves a scan running; it is discarded.</summary>
public sealed class ExpireScansCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ExpireScansCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(ExpireScansCommand request, CancellationToken ct)
    {
        var expired = await factory.ExecuteAsync(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var stale = await ctx.AcquireRepository<IScanRepository>().ListStaleRunningAsync(request.Input.Cutoff, token);
            var now = timeProvider.GetUtcNow();
            foreach (var scan in stale)
                scan.Abort(now, "timed-out");
            return stale.Count;
        }, cancellationToken: ct);

        return Ok(expired);
    }
}
