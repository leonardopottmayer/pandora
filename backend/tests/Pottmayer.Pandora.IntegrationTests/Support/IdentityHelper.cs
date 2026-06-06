using System.Net.Http.Headers;
using System.Net.Http.Json;
using Npgsql;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Helpers for the auth flow: registers a user through the real sign-up endpoint and activates it
/// directly in the database (the activation token is only delivered by e-mail, so tests that just
/// need an authenticatable user flip <c>email_confirmed_at</c> instead of replaying that flow).
/// </summary>
internal static class IdentityHelper
{
    public const string DefaultPassword = "correct horse battery staple";

    private const string SignUpUrl = "/api/v1/identity/auth/signup";
    private const string SignInUrl = "/api/v1/identity/auth/signin";

    public sealed record Tokens(string AccessToken, string RefreshToken);

    public static async Task RegisterActiveUserAsync(
        HttpClient client, string connectionString, string email, string username)
    {
        var response = await client.PostAsJsonAsync(SignUpUrl, new
        {
            name = "Test User",
            username,
            email,
            password = DefaultPassword
        });
        response.EnsureSuccessStatusCode();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE identity.idt001_user SET email_confirmed_at = now() WHERE email = $1";
        cmd.Parameters.AddWithValue(email.ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Registers + activates a user, signs in, and sets the bearer token on the client.</summary>
    public static async Task<Tokens> AuthenticateAsync(
        HttpClient client, string connectionString, string email, string username)
    {
        await RegisterActiveUserAsync(client, connectionString, email, username);
        var tokens = await SignInAsync(client, username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return tokens;
    }

    public static async Task<Tokens> SignInAsync(HttpClient client, string emailOrUsername)
    {
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            emailOrUsername,
            password = DefaultPassword
        });
        response.EnsureSuccessStatusCode();
        return await ReadTokensAsync(response);
    }

    /// <summary>Reads tokens from the Tars success envelope: <c>{ "success": true, "data": { ... } }</c>.</summary>
    public static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>()
                       ?? throw new InvalidOperationException("Response had an empty body.");
        return new Tokens(envelope.Data.AccessToken, envelope.Data.RefreshToken);
    }

    private sealed record Envelope(TokenData Data);
    private sealed record TokenData(string AccessToken, string RefreshToken);
}
