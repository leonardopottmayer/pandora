using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Integrations;

/// <summary>Shared calls and response shapes for the Integrations account tests.</summary>
internal static class AccountsApi
{
    public const string ProvidersUrl = "/api/v1/integrations/providers";
    public const string AccountsUrl = "/api/v1/integrations/accounts";

    public sealed record Envelope<T>(T Data);

    public sealed record ProviderItem(string Provider, string AuthKind, int AccountCount);

    public sealed record UsageItem(string Module, string Purpose, string? Detail);

    public sealed record AccountItem(
        Guid Id, string Provider, string AuthKind, string Label, string? DisplayName, string Status, List<UsageItem> Usages);

    public static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>();
        return envelope!.Data;
    }

    public static async Task<List<ProviderItem>> ProvidersAsync(HttpClient client) =>
        await ReadDataAsync<List<ProviderItem>>(await client.GetAsync(ProvidersUrl));

    public static async Task<List<AccountItem>> AccountsAsync(HttpClient client) =>
        await ReadDataAsync<List<AccountItem>>(await client.GetAsync(AccountsUrl));

    public static async Task<Guid> SaveApiKeyAsync(HttpClient client, string apiKey, string? label = null) =>
        await ReadDataAsync<Guid>(
            await client.PutAsJsonAsync("/api/v1/integrations/gemini/api-key", new { apiKey, label }));

    public static async Task<Guid> UserIdAsync(string connectionString, string email)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM identity.idt001_user WHERE email = $1";
        cmd.Parameters.AddWithValue(email.ToLowerInvariant());
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }
}
