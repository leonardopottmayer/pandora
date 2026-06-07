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
    public const string DefaultPassword = "Str0ng!Pass";

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
        var response = await PostSignInAsync(client, emailOrUsername, DefaultPassword);
        response.EnsureSuccessStatusCode();
        return await ReadSignInTokensAsync(response);
    }

    /// <summary>Posts to the sign-in endpoint without asserting the outcome (so MFA challenges can be inspected).</summary>
    public static Task<HttpResponseMessage> PostSignInAsync(
        HttpClient client, string emailOrUsername, string password)
        => client.PostAsJsonAsync(SignInUrl, new { emailOrUsername, password });

    /// <summary>
    /// Reads tokens from a sign-in response (data is a <c>SignInResultDto</c> whose <c>tokens</c> is set
    /// when MFA is not required).
    /// </summary>
    public static async Task<Tokens> ReadSignInTokensAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<SignInEnvelope>()
                       ?? throw new InvalidOperationException("Response had an empty body.");
        var tokens = envelope.Data.Tokens
                     ?? throw new InvalidOperationException("Sign-in returned an MFA challenge, not tokens.");
        return new Tokens(tokens.AccessToken, tokens.RefreshToken);
    }

    /// <summary>Reads the MFA challenge ticket from a sign-in response, if one was issued.</summary>
    public static async Task<string?> ReadMfaTicketAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<SignInEnvelope>()
                       ?? throw new InvalidOperationException("Response had an empty body.");
        return envelope.Data.Mfa?.Ticket;
    }

    /// <summary>Reads tokens from a plain token envelope: <c>{ "data": { accessToken, refreshToken } }</c> (refresh).</summary>
    public static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>()
                       ?? throw new InvalidOperationException("Response had an empty body.");
        return new Tokens(envelope.Data.AccessToken, envelope.Data.RefreshToken);
    }

    private sealed record Envelope(TokenData Data);
    private sealed record TokenData(string AccessToken, string RefreshToken);
    private sealed record SignInEnvelope(SignInData Data);
    private sealed record SignInData(TokenData? Tokens, MfaChallengeData? Mfa);
    private sealed record MfaChallengeData(string Ticket);
}
