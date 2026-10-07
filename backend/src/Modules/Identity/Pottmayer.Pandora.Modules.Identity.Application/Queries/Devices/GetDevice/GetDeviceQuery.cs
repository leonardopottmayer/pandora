using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevice;

/// <summary>The calling device itself — what <c>GET /identity/devices/me</c> answers to a device key.</summary>
public sealed record GetDeviceInput(Guid DeviceId);

public sealed class GetDeviceQuery(GetDeviceInput input)
    : QueryBase<GetDeviceInput, DeviceDto>(input);
