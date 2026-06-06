using Npgsql;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Applies the repository's <c>migrations/migrations/**.up.sql</c> files directly via SQL, in
/// timestamp order, tracking what has been applied in a private table. Replicates the migration
/// tool's behaviour without depending on its CLI, so the integration database is built from the
/// same scripts the app ships with.
/// </summary>
internal static class SqlMigrationRunner
{
    private const string TrackingTable = "public._integration_migrations";

    public static async Task RunAsync(string connectionString, string migrationsPath)
    {
        if (!Directory.Exists(migrationsPath))
            throw new DirectoryNotFoundException($"Migrations folder not found: '{migrationsPath}'.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await EnsureTrackingTableAsync(connection);
        var applied = await GetAppliedVersionsAsync(connection);

        var pending = Directory
            .GetFiles(migrationsPath, "*.up.sql", SearchOption.AllDirectories)
            .Select(path => new MigrationFile(path))
            .Where(m => !applied.Contains(m.Version))
            .OrderBy(m => m.Version)
            .ToList();

        foreach (var migration in pending)
        {
            var sql = await File.ReadAllTextAsync(migration.FilePath);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();

            await RecordAppliedAsync(connection, migration.Version);
        }
    }

    private static async Task EnsureTrackingTableAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {TrackingTable} (
                version     VARCHAR(50)  PRIMARY KEY,
                applied_at  TIMESTAMPTZ  NOT NULL DEFAULT current_timestamp
            )
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(NpgsqlConnection conn)
    {
        var applied = new HashSet<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT version FROM {TrackingTable}";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            applied.Add(reader.GetString(0));
        return applied;
    }

    private static async Task RecordAppliedAsync(NpgsqlConnection conn, string version)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {TrackingTable} (version) VALUES ($1)";
        cmd.Parameters.AddWithValue(version);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class MigrationFile(string filePath)
    {
        public string FilePath { get; } = filePath;

        // "20260604120001-create-table-not001-notification.up.sql" -> "20260604120001"
        public string Version { get; } = Path.GetFileName(filePath).Split('-')[0];
    }
}
