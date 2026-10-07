using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevices;

public sealed class GetDevicesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetDevicesQuery, IReadOnlyList<DeviceDto>>
{
    protected override async Task<Result<IReadOnlyList<DeviceDto>>> HandleAsync(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        var devices = await factory.ExecuteAsync(IdentityModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IDeviceRepository>().ListActiveByUserAsync(request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return Ok(devices.Select(DeviceDto.From).ToList());
    }
}
