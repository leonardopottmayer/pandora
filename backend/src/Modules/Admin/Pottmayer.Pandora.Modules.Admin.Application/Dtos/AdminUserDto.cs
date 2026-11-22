namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>An account in the users list, with the roles it holds.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Name,
    string Username,
    string Email,
    bool Activated,
    bool Disabled,
    bool AwaitingApproval,
    bool MfaEnabled,
    DateTimeOffset? LastSignInAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> RoleIds);
