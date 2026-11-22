using Pottmayer.Pandora.Modules.Identity.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

public sealed record DeviceDto(
    Guid Id,
    string Name,
    string Platform,
    string Form,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt)
{
    public static DeviceDto From(Device d) =>
        new(d.Id, d.Name, d.Platform.Value, d.Form.Value, d.Scopes, d.CreatedAt, d.LastSeenAt);
}

/// <summary>The pairing response: the device and its key — the only time the key is ever returned.</summary>
public sealed record DeviceRegistrationDto(DeviceDto Device, string Key);
