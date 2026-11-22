using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Desktop.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>What the page shows about this PC's agent (<c>files.status</c>).</summary>
/// <param name="Paired">False when the server rejected the device key (not paired, or revoked).</param>
/// <param name="Enabled">The account switch, as last seen; null before the first contact.</param>
internal sealed record FilesStatus(bool Paired, bool? Enabled, IReadOnlyList<RootConfig> Roots, ScanProgress? Running, string? LastError);

/// <summary>
/// The agent's loop: keeps the configuration fresh, runs each root's daily scan when due, and the scans
/// the page asks for ("Scan now"). One scan at a time — roots often share a disk, and a walk is I/O-bound.
/// </summary>
internal sealed class FilesAgent(FilesApi api, Scanner scanner, IBridgeEvents events, TimeProvider time) : BackgroundService
{
    public const string ProgressEvent = "files.scanProgress";

    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ConfigMaxAge = TimeSpan.FromMinutes(10);

    private readonly Channel<Guid> _requests = Channel.CreateUnbounded<Guid>();
    private readonly Dictionary<Guid, (DateTimeOffset At, bool Failed)> _attempts = [];
    private AgentConfig? _config;
    private DateTimeOffset _configAt;
    private bool _paired = true;
    private ScanProgress? _running;
    private string? _lastError;

    public FilesStatus Status => new(_paired, _config?.Enabled, _config?.Roots ?? [], _running, _lastError);

    /// <summary>Queues a scan of the root; it starts as soon as the current one (if any) ends.</summary>
    public void RequestScan(Guid rootId) => _requests.Writer.TryWrite(rootId);

    /// <summary>Fetches the configuration now — after the user edits roots, selection or filters.</summary>
    public async Task<FilesStatus> RefreshAsync(CancellationToken ct)
    {
        await TryRefreshConfigAsync(ct);
        return Status;
    }

    public RootConfig? FindRoot(Guid rootId) => _config?.Roots.FirstOrDefault(r => r.Id == rootId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await NextRequestAsync(stoppingToken) is { } requested)
                {
                    // Scan now: always on fresh configuration, and regardless of the schedule.
                    if (await TryRefreshConfigAsync(stoppingToken) && FindRoot(requested) is { } root)
                        await RunAsync(root, stoppingToken);
                    continue;
                }

                if (time.GetUtcNow() - _configAt >= ConfigMaxAge && !await TryRefreshConfigAsync(stoppingToken)) continue;

                foreach (var root in _config?.Roots ?? [])
                {
                    var attempt = _attempts.TryGetValue(root.Id, out var a) ? a : default((DateTimeOffset At, bool Failed)?);
                    if (ScanSchedule.IsDue(root.ScanTime, root.LastCompletedScanAt, attempt?.At, attempt?.Failed ?? false, time.GetLocalNow()))
                        await RunAsync(root, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunAsync(RootConfig root, CancellationToken ct)
    {
        var failed = false;
        try
        {
            var outcome = await scanner.RunAsync(root, Report, ct);
            Report(new ScanProgress(root.Id, outcome.Id, outcome.Status, _running?.Walked ?? 0, _running?.Fingerprinted ?? 0,
                outcome.Error, outcome));
            _lastError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            failed = true;
            Fail(ex);
            Report(new ScanProgress(root.Id, _running?.ScanId, "failed", _running?.Walked ?? 0, _running?.Fingerprinted ?? 0, ex.Message));
        }
        finally
        {
            _attempts[root.Id] = (time.GetLocalNow(), failed);
            _running = null;
        }

        // The completed scan moved LastCompletedScanAt on the server; read it before scheduling again.
        await TryRefreshConfigAsync(ct);
    }

    private void Report(ScanProgress progress)
    {
        _running = progress;
        events.Publish(ProgressEvent, progress);
    }

    private async Task<bool> TryRefreshConfigAsync(CancellationToken ct)
    {
        try
        {
            _config = await api.GetConfigAsync(ct);
            _configAt = time.GetUtcNow();
            _paired = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Fail(ex);
            return false;
        }
    }

    private void Fail(Exception ex)
    {
        if (ex is FilesApiException { IsUnauthorized: true }) _paired = false;
        _lastError = ex.Message;
        Trace.TraceWarning($"Files agent: {ex.Message}");
    }

    /// <summary>The next "Scan now" request, or null when a tick passes without one.</summary>
    private async Task<Guid?> NextRequestAsync(CancellationToken ct)
    {
        using var tick = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tick.CancelAfter(Tick);
        try
        {
            return await _requests.Reader.ReadAsync(tick.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
