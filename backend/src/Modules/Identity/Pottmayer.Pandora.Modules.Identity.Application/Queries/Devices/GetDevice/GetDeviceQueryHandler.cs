using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevice;

public sealed class GetDeviceQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetDeviceQuery, DeviceDto>
{
    protected override async Task<Result<DeviceDto>> HandleAsync(GetDeviceQuery request, CancellationToken cancellationToken)
    {
        var device = await factory.ExecuteAsync(IdentityModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IDeviceRepository>().GetByIdAsync(request.Input.DeviceId, ct),
            cancellationToken: cancellationToken);

        return device is { IsActive: true } ? Ok(DeviceDto.From(device)) : Fail(IdentityErrors.DeviceNotFound);
    }
}
