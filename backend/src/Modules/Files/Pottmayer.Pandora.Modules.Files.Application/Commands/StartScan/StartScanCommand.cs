using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.StartScan;

public sealed record StartScanInput(Guid UserId, Guid DeviceId, Guid RootId);

public sealed class StartScanCommand(StartScanInput input)
    : CommandBase<StartScanInput, StartScanResponse>(input);
