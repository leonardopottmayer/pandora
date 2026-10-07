using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Devices;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RegisterDevice;

/// <summary>
/// Pairs a device for the signed-in user and returns its key once. Scopes are not checked against a
/// catalog: the user grants their own device access to their own data, and a scope only matters where
/// an endpoint asks for it.
/// </summary>
public sealed class RegisterDeviceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RegisterDeviceCommand, DeviceRegistrationDto>
{
    protected override async Task<Result<DeviceRegistrationDto>> HandleAsync(RegisterDeviceCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var scopes = input.Scopes ?? [];

        if (!Device.IsValidName(input.Name)) return Fail(IdentityErrors.InvalidDeviceName);
        if (!DevicePlatform.IsSupported(input.Platform)) return Fail(IdentityErrors.InvalidDevicePlatform);
        if (!DeviceForm.IsSupported(input.Form)) return Fail(IdentityErrors.InvalidDeviceForm);
        if (!scopes.All(Device.IsValidScope)) return Fail(IdentityErrors.InvalidDeviceScope);

        var key = DeviceKeys.Generate();
        var device = Device.Register(
            input.UserId, input.Name!, DevicePlatform.FromValue(input.Platform!), DeviceForm.FromValue(input.Form!),
            DeviceKeys.Hash(key), scopes, timeProvider.GetUtcNow());

        await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            await ctx.AcquireRepository<IDeviceRepository>().AddAsync(device, token);
            return true;
        }, cancellationToken: ct);

        return Ok(new DeviceRegistrationDto(DeviceDto.From(device), key));
    }
}
