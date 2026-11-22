using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class WeekStartConverter()
    : ValueConverter<DayOfWeek, string>(
        v => v.ToString().ToLowerInvariant(),
        s => Enum.Parse<DayOfWeek>(s, ignoreCase: true));
