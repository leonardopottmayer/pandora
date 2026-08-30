using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pottmayer.Tars.Communication.Email.Abstractions;
using Pottmayer.Tars.Communication.Email.DI;
using Pottmayer.Tars.Messaging.Broker.Dispatch;
using Pottmayer.Tars.Messaging.Broker.Registry;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Options;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Relay;
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
        // fin002 (system categories) and fin012 (import layouts) hold seeded reference data the
        // migrations create once — preserve them across resets or imports can't detect a layout.
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["identity", "channels", "finances", "notes", "agenda"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("finances", "fin002_system_category"),
                new Respawn.Graph.Table("finances", "fin012_import_layout")
            ]
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
                ["Tars:Data:Connections:channels:ConnectionString"] = ConnectionString,
                ["Tars:Data:Connections:finances:ConnectionString"] = ConnectionString,
                ["Tars:Data:Connections:notes:ConnectionString"] = ConnectionString,
                ["Tars:Data:Connections:agenda:ConnectionString"] = ConnectionString,
                // A bot username is all the linking flow needs; no token, because nothing calls Telegram here.
                ["Pandora:Channels:Telegram:BotUsername"] = "pandora_test_bot",
                // Fixed AES-256 key (Base64 of 32 bytes) so MFA secrets can be encrypted in tests.
                ["Pandora:Identity:Mfa:EncryptionKey"] = "7Mzi45PyKOyGH1hWmXvnDKCVOY9qKeEB8P8NTuZe3T4="
            });
        });

        builder.ConfigureServices(services =>
        {
            // Drop the periodic dispatcher: tests drive dispatch explicitly so timing is deterministic.
            services.RemoveAll<IHostedService>();

            // Force the logging e-mail sender. The module picks its provider at registration time
            // from configuration (appsettings says "mailkit"), which runs before the test's in-memory
            // config is layered on — so overriding "Provider" there cannot switch it. Swapping the
            // resolved service here is the override point that actually takes effect, and keeps the
            // suite off a live SMTP server.
            services.RemoveAll<IEmailSender>();
            services.AddTarsLoggingEmailSender();
        });
    }

    /// <summary>Wipes business data so each test starts from a clean slate.</summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    /// <summary>
    /// Drains the transactional outbox synchronously, standing in for the background relay (which the
    /// harness removes for determinism). After a producing action, call this before asserting on the
    /// downstream effect — the outbox row is written in the producer's transaction, and this delivers
    /// it to the handlers here and now.
    /// </summary>
    public async Task DrainOutboxAsync(params string[] databaseKeys)
    {
        var keys = databaseKeys.Length > 0 ? databaseKeys : ["identity", "channels", "agenda"];

        foreach (var key in keys)
        {
            var processor = new OutboxRelayProcessor(
                Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<IIntegrationEventTypeRegistry>(),
                Services.GetRequiredService<IIntegrationEventDispatcher>(),
                Services.GetRequiredService<IIntegrationEventSerializer>(),
                Services.GetRequiredService<TimeProvider>(),
                NullLogger.Instance,
                new OutboxDatabaseOptions(key) { PurgeEnabled = false });

            // Drain until the outbox is empty: a batch-sized pass may leave more behind.
            while (await processor.DrainOnceAsync() > 0) { }
        }
    }

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
