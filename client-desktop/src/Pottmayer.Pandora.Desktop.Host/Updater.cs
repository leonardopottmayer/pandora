using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// Checks GitHub Releases at startup and every few hours, and downloads a newer version in the background.
/// Velopack applies a downloaded update on the next start (its auto-apply on startup is on by default).
/// </summary>
internal static class Updater
{
    private const string RepoUrl = "https://github.com/leonardopottmayer/pandora";

    /// <summary>A local folder of packages (vpk's output) to update from instead of GitHub — for testing updates.</summary>
    private const string LocalSourceVariable = "PANDORA_DESKTOP_UPDATES";

    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        var local = Environment.GetEnvironmentVariable(LocalSourceVariable);
        var manager = string.IsNullOrWhiteSpace(local)
            ? new UpdateManager(new GithubSource(RepoUrl, null, false))
            : new UpdateManager(local);
        if (!manager.IsInstalled) return; // running from bin/ during development

        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    var update = await manager.CheckForUpdatesAsync();
                    if (update is null) continue;
                    await manager.DownloadUpdatesAsync(update, null, cancellationToken);
                    return; // downloaded: it applies on the next start
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed check leaves the current version running; the next tick retries.
                    Trace.TraceWarning($"Update check failed: {ex.Message}");
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
