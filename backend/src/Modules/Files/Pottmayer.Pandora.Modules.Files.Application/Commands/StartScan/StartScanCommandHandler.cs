using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.StartScan;

/// <summary>
/// Starts a scan of one of the device's roots. Only this device scans the root, so a new start means any
/// earlier run is dead (a crashed agent) or stale (a held scan): those are aborted as <c>superseded</c>,
/// never applied.
/// </summary>
public sealed class StartScanCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<StartScanCommand, StartScanResponse>
{
    protected override async Task<Result<StartScanResponse>> HandleAsync(StartScanCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        // Its own unit of work: the old run must be closed before the new one is inserted (one running scan per root).
        var check = await factory.ExecuteAsync<Result<bool>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var preferences = await ctx.AcquireRepository<IPreferencesRepository>().GetByIdAsync(input.UserId, token);
            if (preferences is not { IsEnabled: true }) return FilesErrors.NotEnabled;

            var root = await ctx.AcquireRepository<IRootRepository>().FindActiveForDeviceAsync(input.RootId, input.DeviceId, token);
            if (root is null) return FilesErrors.RootNotFound;

            foreach (var open in await ctx.AcquireRepository<IScanRepository>().ListOpenByRootAsync(root.Id, token))
                open.Abort(now, "superseded");
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
        if (check.IsFailure) return Fail(check.Errors);

        var scan = Scan.Start(input.RootId, now);
        await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IScanRepository>().AddAsync(scan, token), cancellationToken: ct);

        return Ok(new StartScanResponse(scan.Id));
    }
}
