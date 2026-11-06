namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// Who is who, for the modules that manage users without owning them (Admin): finds an account by its
/// e-mail address. Identity stays the owner of users; nothing outside it reads its tables.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The id of the activated account with this e-mail, or <c>null</c> when there is none yet (not
    /// registered, or registered but not activated).
    /// </summary>
    Task<Guid?> FindActivatedUserIdByEmailAsync(string email, CancellationToken ct = default);
}
