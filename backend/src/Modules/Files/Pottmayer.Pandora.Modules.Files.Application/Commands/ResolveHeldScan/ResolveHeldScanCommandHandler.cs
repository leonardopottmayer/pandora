using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.ResolveHeldScan;

/// <summary>
/// The user's answer to the safety brake. Confirming applies the scan as if the brake had not fired (the
/// entries go to the review inbox, still not deleted); discarding drops it, and nothing is marked.
/// </summary>
public sealed class ResolveHeldScanCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ResolveHeldScanCommand, ScanDto>
{
    protected override async Task<Result<ScanDto>> HandleAsync(ResolveHeldScanCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<ScanDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var scan = await ctx.AcquireRepository<IScanRepository>().GetByIdAsync(input.ScanId, token);
            var root = scan is null ? null
                : await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(scan.RootId, input.UserId, token);
            if (scan is null || root is null) return FilesErrors.ScanNotFound;
            if (!scan.IsHeld) return FilesErrors.ScanNotHeld;

            var now = timeProvider.GetUtcNow();
            if (input.Apply) await ScanApplier.ApplyAsync(ctx, root, scan, enforceBrake: false, now, token);
            else scan.Abort(now, "discarded");

            return Result<ScanDto>.Success(ScanDto.From(scan));
        }, cancellationToken: ct);
    }
}
