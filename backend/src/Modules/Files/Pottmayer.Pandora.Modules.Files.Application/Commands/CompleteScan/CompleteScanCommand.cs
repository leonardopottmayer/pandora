using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.CompleteScan;

public sealed record CompleteScanInput(Guid UserId, Guid DeviceId, Guid ScanId, int EntriesSeen);

[RequiresPermission(FilesPermissions.Scans.Run)]
public sealed class CompleteScanCommand(CompleteScanInput input)
    : CommandBase<CompleteScanInput, ScanDto>(input);
