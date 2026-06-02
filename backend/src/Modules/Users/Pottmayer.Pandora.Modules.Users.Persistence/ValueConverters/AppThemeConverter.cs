using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Users.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Users.Persistence.ValueConverters;

internal sealed class AppThemeConverter()
    : ValueConverter<AppTheme, string>(v => v.Value, s => AppTheme.FromValue(s));
