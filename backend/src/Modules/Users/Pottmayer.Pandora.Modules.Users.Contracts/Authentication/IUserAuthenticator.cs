namespace Pottmayer.Pandora.Modules.Users.Contracts.Authentication;

public interface IUserAuthenticator
{
    Task<UserAuthResult> AuthenticateAsync(string emailOrUsername, string password, CancellationToken ct = default);
}
