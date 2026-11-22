using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.AbortScan;

/// <summary>The agent gives up (e.g. <c>root-unavailable</c>): nothing is marked missing. Entries it created stay — they were real.</summary>
public sealed class AbortScanCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<AbortScanCommand, ScanDto>
{
    protected override async Task<Result<ScanDto>> HandleAsync(AbortScanCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<ScanDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var found = await AgentScans.FindRunningAsync(ctx, input.DeviceId, input.ScanId, token);
            if (found.IsFailure) return Result<ScanDto>.Failure(found.Errors);
            var (scan, _) = found.Value;

            scan.Abort(timeProvider.GetUtcNow(), string.IsNullOrWhiteSpace(input.Reason) ? "aborted" : input.Reason.Trim());
            return Result<ScanDto>.Success(ScanDto.From(scan));
        }, cancellationToken: ct);
    }
}
