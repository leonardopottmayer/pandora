namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// Who is who, for the modules that manage users without owning them (Admin): finds, lists and
/// disables accounts. Identity stays the owner of users; nothing outside it reads its tables.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The id of the activated account with this e-mail, or <c>null</c> when there is none yet (not
    /// registered, or registered but not activated).
    /// </summary>
    Task<Guid?> FindActivatedUserIdByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Every account, newest first.</summary>
    Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default);

    /// <summary>The account with this id, or <c>null</c>.</summary>
    Task<DirectoryUser?> FindAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Disables or enables the account. Disabling also ends its sessions. <c>false</c> when there is no
    /// such account.
    /// </summary>
    Task<bool> SetDisabledAsync(Guid userId, bool disabled, CancellationToken ct = default);

    /// <summary>
    /// Lets an account waiting for approval sign in and tells the user. <c>false</c> when there is no
    /// such account; an account that was not waiting is left as it is.
    /// </summary>
    Task<bool> ApproveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Ends every session of the account (its refresh tokens). <c>false</c> when there is no such account.</summary>
    Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken ct = default);
}
