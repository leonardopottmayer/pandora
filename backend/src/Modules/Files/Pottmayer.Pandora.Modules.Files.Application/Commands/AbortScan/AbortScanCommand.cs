using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.AbortScan;

public sealed record AbortScanInput(Guid UserId, Guid DeviceId, Guid ScanId, string? Reason);

[RequiresPermission(FilesPermissions.Scans.Run)]
public sealed class AbortScanCommand(AbortScanInput input)
    : CommandBase<AbortScanInput, ScanDto>(input);
