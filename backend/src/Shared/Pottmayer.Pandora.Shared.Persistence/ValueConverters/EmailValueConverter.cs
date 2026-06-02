using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;

namespace Pottmayer.Pandora.Shared.Persistence.ValueConverters;

public sealed class EmailValueConverter()
    : ValueConverter<Email, string>(v => v.Value, s => Email.FromValue(s));
