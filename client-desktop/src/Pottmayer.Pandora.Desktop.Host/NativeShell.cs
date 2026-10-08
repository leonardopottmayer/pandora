using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Desktop.Abstractions;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>The Windows side of <see cref="IDesktopShell"/>: the folder dialog over the window, and Explorer.</summary>
internal sealed class WindowsShell(IServiceProvider services) : IDesktopShell
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        // Resolved lazily: the window is built after the modules that use this.
        var form = services.GetRequiredService<MainForm>();
        var result = new TaskCompletionSource<string?>();
        form.BeginInvoke(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = ShellText.PickFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            result.SetResult(dialog.ShowDialog(form) == DialogResult.OK ? dialog.SelectedPath : null);
        });
        return result.Task;
    }

    public void Reveal(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"")
        {
            UseShellExecute = true,
        });
}
