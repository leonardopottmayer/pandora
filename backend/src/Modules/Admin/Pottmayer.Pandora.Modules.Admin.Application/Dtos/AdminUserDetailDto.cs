namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>One account: its roles, its exceptions and every permission any of them touches.</summary>
public sealed record AdminUserDetailDto(
    AdminUserDto User,
    IReadOnlyList<UserExceptionDto> Exceptions,
    IReadOnlyList<EffectivePermissionDto> Permissions);

/// <param name="Effect"><c>allow</c> or <c>deny</c>.</param>
public sealed record UserExceptionDto(string Pattern, string Effect);

/// <summary>A permission and where it comes from. A deny beats the roles and the allows.</summary>
/// <param name="Roles">Names of the user's roles that grant it.</param>
/// <param name="Allowed">A direct allow covers it.</param>
/// <param name="Denied">A direct deny covers it.</param>
public sealed record EffectivePermissionDto(string Key, bool Granted, IReadOnlyList<string> Roles, bool Allowed, bool Denied);
