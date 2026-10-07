using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class DeviceFormConverter()
    : ValueConverter<DeviceForm, string>(v => v.Value, s => DeviceForm.FromValue(s));
