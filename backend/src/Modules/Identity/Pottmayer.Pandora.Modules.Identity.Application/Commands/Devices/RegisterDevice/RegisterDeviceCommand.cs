using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RegisterDevice;

public sealed record RegisterDeviceInput(
    Guid UserId, string? Name, string? Platform, string? Form, IReadOnlyList<string>? Scopes);

public sealed class RegisterDeviceCommand(RegisterDeviceInput input)
    : CommandBase<RegisterDeviceInput, DeviceRegistrationDto>(input);
