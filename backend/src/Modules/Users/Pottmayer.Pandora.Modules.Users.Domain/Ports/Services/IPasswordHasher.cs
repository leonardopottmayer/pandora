namespace Pottmayer.Pandora.Modules.Users.Domain.Ports.Services;

public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}
