using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.AbortScan;

public sealed record AbortScanInput(Guid UserId, Guid DeviceId, Guid ScanId, string? Reason);

public sealed class AbortScanCommand(AbortScanInput input)
    : CommandBase<AbortScanInput, ScanDto>(input);
