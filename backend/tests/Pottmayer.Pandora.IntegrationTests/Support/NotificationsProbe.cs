using Npgsql;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>Reads the <c>notifications.not001_notification</c> table directly, for asserting on persisted state.</summary>
internal sealed class NotificationsProbe(string connectionString)
{
    public sealed record Row(string Recipient, string TemplateKey, string Locale, string Status, string? Provider, int AttemptCount);

    public async Task<int> CountAsync()
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM notifications.not001_notification";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<Row?> FindByRecipientAsync(string recipient)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT recipient, template_key, locale, status, provider, attempt_count
            FROM notifications.not001_notification
            WHERE recipient = $1
            """;
        cmd.Parameters.AddWithValue(recipient);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Row(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt32(5));
    }

    /// <summary>Reads the plaintext activation token carried in the notification payload (delivered by e-mail).</summary>
    public async Task<string> GetActivationTokenAsync(string recipient)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload->>'token' FROM notifications.not001_notification WHERE recipient = $1";
        cmd.Parameters.AddWithValue(recipient);
        return await cmd.ExecuteScalarAsync() as string
               ?? throw new InvalidOperationException($"No activation token found for '{recipient}'.");
    }

    /// <summary>Polls for a notification to the given recipient, tolerating async event dispatch.</summary>
    public async Task<Row> WaitForRecipientAsync(string recipient, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (await FindByRecipientAsync(recipient) is { } row)
                return row;
            await Task.Delay(50);
        }

        throw new TimeoutException($"No notification for '{recipient}' was persisted within the timeout.");
    }

    /// <summary>Polls for a notification to the given recipient using a specific template.</summary>
    public async Task<Row> WaitForTemplateAsync(string recipient, string templateKey, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            await using var conn = await OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT recipient, template_key, locale, status, provider, attempt_count
                FROM notifications.not001_notification
                WHERE recipient = $1 AND template_key = $2
                """;
            cmd.Parameters.AddWithValue(recipient);
            cmd.Parameters.AddWithValue(templateKey);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Row(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5));
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No '{templateKey}' notification for '{recipient}' was persisted within the timeout.");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
