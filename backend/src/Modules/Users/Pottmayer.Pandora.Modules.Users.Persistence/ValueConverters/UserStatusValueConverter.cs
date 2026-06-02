using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pottmayer.Pandora.Modules.Users.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Users.Persistence.ValueConverters;

internal sealed class UserStatusConverter()
    : ValueConverter<UserStatus, string>(v => v.Value, s => UserStatus.FromValue(s));
