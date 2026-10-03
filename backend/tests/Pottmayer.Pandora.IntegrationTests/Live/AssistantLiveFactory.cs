using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;
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

namespace Pottmayer.Pandora.IntegrationTests.Live;

/// <summary>
/// The real Host against a throwaway PostgreSQL loaded with the personal seed
/// (<c>migrations/seed/seed.local.sql</c>), talking to the real Gemini with the seeded key. Only the
/// Telegram transport is replaced — by <see cref="Telegram"/>, which records what would have been sent —
/// so nothing reaches the user's chat. Background jobs are off: no sweep fires a reminder mid-run.
/// </summary>
public sealed class AssistantLiveFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("pandora_live")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner _respawner = default!;

    public string ConnectionString => _postgres.GetConnectionString();

    public CapturingTelegramSender Telegram { get; } = new();

    public static string? SeedPath => FindRepoFile("migrations", "seed", "seed.local.sql");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await SqlMigrationRunner.RunAsync(ConnectionString, FindRepoFile("migrations", "migrations")!);

        await using var connection = await OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["identity", "channels", "finances", "notes", "agenda", "integrations", "assistant"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("finances", "fin002_system_category"),
                new Respawn.Graph.Table("finances", "fin012_import_layout"),
            ],
        });
    }

    /// <summary>Wipes everything and loads the seed again, dated from today.</summary>
    public async Task ReseedAsync()
    {
        await using var connection = await OpenAsync();
        await _respawner.ResetAsync(connection);
        await using var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(SeedPath!), connection);
        await cmd.ExecuteNonQueryAsync();
        Telegram.Clear();
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
                new[] { "identity", "channels", "finances", "notes", "agenda", "integrations", "assistant" }
                    .ToDictionary(key => $"Tars:Data:Connections:{key}:ConnectionString", _ => (string?)ConnectionString)));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailSender>();
            services.AddTarsLoggingEmailSender();
            services.RemoveAll<ITelegramSender>();
            services.AddSingleton<ITelegramSender>(Telegram);
        });
    }

    /// <summary>Delivers what the outboxes hold to their handlers, standing in for the background relay.</summary>
    public async Task DrainOutboxAsync(params string[] databaseKeys)
    {
        foreach (var key in databaseKeys)
        {
            var processor = new OutboxRelayProcessor(
                Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<IIntegrationEventTypeRegistry>(),
                Services.GetService<IOutboxRelayDelivery>()
                    ?? new LocalHandlerOutboxDelivery(Services.GetRequiredService<IIntegrationEventDispatcher>()),
                Services.GetRequiredService<IIntegrationEventSerializer>(),
                Services.GetRequiredService<TimeProvider>(),
                NullLogger.Instance,
                new OutboxDatabaseOptions(key) { PurgeEnabled = false });

            while (await processor.DrainOnceAsync() > 0) { }
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private static string? FindRepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "migrations", "config.json")))
                continue;
            var path = Path.Combine([dir.FullName, .. parts]);
            return File.Exists(path) || Directory.Exists(path) ? path : null;
        }
        return null;
    }
}

/// <summary>Records every Telegram message instead of sending it.</summary>
public sealed class CapturingTelegramSender : ITelegramSender
{
    private readonly ConcurrentQueue<(string Text, IReadOnlyList<TelegramRenderedButton> Buttons)> _sent = new();

    public IReadOnlyList<(string Text, IReadOnlyList<TelegramRenderedButton> Buttons)> Sent => [.. _sent];

    public void Clear() => _sent.Clear();

    public Task SendAsync(string bot, string chatId, string text,
        IReadOnlyList<TelegramRenderedButton>? buttons = null, CancellationToken ct = default)
    {
        _sent.Enqueue((text, buttons ?? []));
        return Task.CompletedTask;
    }
}

[CollectionDefinition("AssistantLive")]
public sealed class AssistantLiveCollection : ICollectionFixture<AssistantLiveFactory>;

/// <summary>Runs only with <c>PANDORA_LIVE_GEMINI=1</c> and the personal seed present: it spends real tokens.</summary>
public sealed class LiveGeminiFactAttribute : FactAttribute
{
    public LiveGeminiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PANDORA_LIVE_GEMINI") != "1")
            Skip = "Set PANDORA_LIVE_GEMINI=1 to talk to the real Gemini.";
        else if (AssistantLiveFactory.SeedPath is null)
            Skip = "migrations/seed/seed.local.sql is missing.";
    }
}
