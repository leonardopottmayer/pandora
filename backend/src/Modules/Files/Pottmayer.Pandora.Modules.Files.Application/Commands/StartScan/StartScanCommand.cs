using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.StartScan;

public sealed record StartScanInput(Guid UserId, Guid DeviceId, Guid RootId);

[RequiresPermission(FilesPermissions.Scans.Run)]
public sealed class StartScanCommand(StartScanInput input)
    : CommandBase<StartScanInput, StartScanResponse>(input);
