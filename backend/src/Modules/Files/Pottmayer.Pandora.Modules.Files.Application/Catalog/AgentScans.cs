using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Files.Application.Catalog;

internal static class AgentScans
{
    /// <summary>A running scan of an active root of this device; any other device's scan is "not found".</summary>
    public static async Task<Result<(Scan Scan, Root Root)>> FindRunningAsync(
        IDataContext ctx, Guid deviceId, Guid scanId, CancellationToken ct)
    {
        var scan = await ctx.AcquireRepository<IScanRepository>().GetByIdAsync(scanId, ct);
        var root = scan is null ? null
            : await ctx.AcquireRepository<IRootRepository>().FindActiveForDeviceAsync(scan.RootId, deviceId, ct);
        if (scan is null || root is null) return FilesErrors.ScanNotFound;
        if (!scan.IsRunning) return FilesErrors.ScanNotRunning;

        return Result<(Scan, Root)>.Success((scan, root));
    }
}
