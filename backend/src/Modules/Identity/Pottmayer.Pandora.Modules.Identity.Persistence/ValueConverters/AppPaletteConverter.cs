using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class AppPaletteConverter()
    : ValueConverter<AppPalette, string>(v => v.Value, s => AppPalette.FromValue(s));
