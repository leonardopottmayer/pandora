using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class AppThemeConverter()
    : ValueConverter<AppTheme, string>(v => v.Value, s => AppTheme.FromValue(s));
