using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class DevicePlatformConverter()
    : ValueConverter<DevicePlatform, string>(v => v.Value, s => DevicePlatform.FromValue(s));
