using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevices;

public sealed record GetDevicesInput(Guid UserId);

public sealed class GetDevicesQuery(GetDevicesInput input)
    : QueryBase<GetDevicesInput, IReadOnlyList<DeviceDto>>(input);
