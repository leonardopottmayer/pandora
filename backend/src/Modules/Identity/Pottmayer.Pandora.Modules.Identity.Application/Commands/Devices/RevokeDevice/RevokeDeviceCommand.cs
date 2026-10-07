using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RevokeDevice;

public sealed record RevokeDeviceInput(Guid UserId, Guid DeviceId);

public sealed class RevokeDeviceCommand(RevokeDeviceInput input)
    : CommandBase<RevokeDeviceInput, bool>(input);
