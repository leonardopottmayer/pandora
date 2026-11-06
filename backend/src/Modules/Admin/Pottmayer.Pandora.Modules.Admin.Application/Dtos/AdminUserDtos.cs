using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

internal static class AdminUserDtos
{
    public static AdminUserDto From(DirectoryUser user, IReadOnlyList<Guid> roleIds) => new(
        user.Id, user.Name, user.Username, user.Email, user.Activated, user.Disabled, user.MfaEnabled,
        user.LastSignInAt, user.CreatedAt, roleIds);
}
