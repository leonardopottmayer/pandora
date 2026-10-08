using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.CompleteScan;

public sealed record CompleteScanInput(Guid UserId, Guid DeviceId, Guid ScanId, int EntriesSeen);

public sealed class CompleteScanCommand(CompleteScanInput input)
    : CommandBase<CompleteScanInput, ScanDto>(input);
