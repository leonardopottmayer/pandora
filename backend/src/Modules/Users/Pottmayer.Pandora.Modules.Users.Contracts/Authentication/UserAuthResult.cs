namespace Pottmayer.Pandora.Modules.Users.Contracts.Authentication;

public sealed record UserAuthResult(UserAuthStatus Status, UserAuthDto? User)
{
    public bool IsSuccess => Status == UserAuthStatus.Success;

    public static UserAuthResult Success(UserAuthDto user)       => new(UserAuthStatus.Success, user);
    public static UserAuthResult InvalidCredentials()            => new(UserAuthStatus.InvalidCredentials, null);
    public static UserAuthResult AccountNotActive()              => new(UserAuthStatus.AccountNotActive, null);
}
