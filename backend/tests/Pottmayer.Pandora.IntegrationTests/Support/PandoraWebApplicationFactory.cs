using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Shared harness for the integration suite: spins up a real PostgreSQL via Testcontainers, builds
/// the schema from the repository's SQL migrations, boots the real Host, and resets business data
/// between tests with Respawn.
/// </summary>
public sealed class PandoraWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("pandora_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlConnection _connection = default!;
    private Respawner _respawner = default!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await SqlMigrationRunner.RunAsync(ConnectionString, ResolveMigrationsPath());

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        // Resets only the module schemas; the migration tracking table lives in "public" and is left intact.
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["identity", "notifications"]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tars:Data:Connections:identity:ConnectionString"] = ConnectionString,
                ["Tars:Data:Connections:notifications:ConnectionString"] = ConnectionString,
                // Deliver e-mails to the log (always succeeds) instead of SMTP — no Mailpit needed.
                ["Communication:Email:Provider"] = "logging"
            });
        });

        // Drop the periodic dispatcher: tests drive dispatch explicitly so timing is deterministic.
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
    }

    /// <summary>Wipes business data so each test starts from a clean slate.</summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Walks up from the test output directory to the repository root (the folder that holds
    /// <c>migrations/config.json</c>) and returns its <c>migrations/migrations</c> folder.
    /// Override with the <c>PANDORA_MIGRATIONS_PATH</c> environment variable (e.g. in CI).
    /// </summary>
    private static string ResolveMigrationsPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PANDORA_MIGRATIONS_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "config.json")))
                return Path.Combine(dir.FullName, "migrations", "migrations");
        }

        throw new InvalidOperationException(
            "Could not locate the repository's 'migrations' folder. " +
            "Set PANDORA_MIGRATIONS_PATH to the absolute path of 'migrations/migrations'.");
    }
}
