namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>An account as other modules see it, through <see cref="IUserDirectory"/>.</summary>
public sealed record DirectoryUser(
    Guid Id,
    string Name,
    string Username,
    string Email,
    bool Activated,
    bool Disabled,
    bool AwaitingApproval,
    bool MfaEnabled,
    DateTimeOffset? LastSignInAt,
    DateTimeOffset CreatedAt);
