using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.ValueConverters;

internal sealed class AppLanguageConverter()
    : ValueConverter<AppLanguage, string>(v => v.Value, s => AppLanguage.FromValue(s));
