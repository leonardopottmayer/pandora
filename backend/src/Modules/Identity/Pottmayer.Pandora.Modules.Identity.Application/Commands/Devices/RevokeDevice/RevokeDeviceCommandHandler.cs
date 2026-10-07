using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RevokeDevice;

/// <summary>Revokes a device: its key stops working on the next request. Another user's device is "not found".</summary>
public sealed class RevokeDeviceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RevokeDeviceCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RevokeDeviceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var revoked = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var devices = ctx.AcquireRepository<IDeviceRepository>();
            var device = await devices.GetByIdAsync(input.DeviceId, token);
            if (device is null || device.UserId != input.UserId || !device.IsActive) return false;

            device.Revoke(timeProvider.GetUtcNow());
            await devices.UpdateAsync(device, token);
            return true;
        }, cancellationToken: ct);

        return revoked ? Ok(true) : Fail(IdentityErrors.DeviceNotFound);
    }
}
