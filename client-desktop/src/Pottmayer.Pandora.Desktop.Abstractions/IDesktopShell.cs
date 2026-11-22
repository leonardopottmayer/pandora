namespace Pottmayer.Pandora.Desktop.Abstractions;

/// <summary>
/// The native bits only the shell can do — the OS dialogs and file manager. Modules stay free of any OS
/// reference and ask the shell instead; a Linux or macOS shell implements the same interface.
/// </summary>
public interface IDesktopShell
{
    /// <summary>The OS folder picker, over the app window. Null when the user cancels.</summary>
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);

    /// <summary>Shows a file (selected in its folder) or a folder in the OS file manager.</summary>
    void Reveal(string path);
}
