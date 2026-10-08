using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.CompleteScan;

/// <summary>
/// The walk is over: the scan is applied (see <see cref="ScanApplier"/>), or held by the safety brake.
/// If the agent counted more entries than arrived, a batch was lost — the scan is aborted rather than
/// marking the lost entries missing.
/// </summary>
public sealed class CompleteScanCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CompleteScanCommand, ScanDto>
{
    protected override async Task<Result<ScanDto>> HandleAsync(CompleteScanCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<ScanDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var found = await AgentScans.FindRunningAsync(ctx, input.DeviceId, input.ScanId, token);
            if (found.IsFailure) return Result<ScanDto>.Failure(found.Errors);
            var (scan, root) = found.Value;

            var now = timeProvider.GetUtcNow();
            if (input.EntriesSeen != scan.Seen) scan.Abort(now, "entries-seen-mismatch");
            else await ScanApplier.ApplyAsync(ctx, root, scan, enforceBrake: true, now, token);

            return Result<ScanDto>.Success(ScanDto.From(scan));
        }, cancellationToken: ct);
    }
}
