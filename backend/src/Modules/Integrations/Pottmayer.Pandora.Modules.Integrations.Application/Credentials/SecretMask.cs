namespace Pottmayer.Pandora.Modules.Integrations.Application.Credentials;

/// <summary>A non-secret hint for settings: bullets plus the last four characters of a key.</summary>
public static class SecretMask
{
    public static string Of(string secret)
    {
        const string bullets = "••••";
        return secret.Length <= 4 ? bullets : bullets + secret[^4..];
    }
}
