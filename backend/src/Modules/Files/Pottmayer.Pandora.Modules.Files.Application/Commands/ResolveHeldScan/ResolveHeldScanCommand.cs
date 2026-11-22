using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.ResolveHeldScan;

/// <param name="Apply">True confirms the scan (marks the entries missing); false discards it.</param>
public sealed record ResolveHeldScanInput(Guid UserId, Guid ScanId, bool Apply);

[RequiresPermission(FilesPermissions.Scans.Run)]
public sealed class ResolveHeldScanCommand(ResolveHeldScanInput input)
    : CommandBase<ResolveHeldScanInput, ScanDto>(input);
