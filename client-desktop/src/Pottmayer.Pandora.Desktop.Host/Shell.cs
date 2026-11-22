using System.Globalization;
using System.Reflection;
using Pottmayer.Pandora.Desktop.Abstractions;
using Pottmayer.Pandora.Desktop.Files;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// The shell's whole knowledge of features: a list. Adding a module is a line here, never a change to
/// the shell. Each one is off until switched on for this PC.
/// </summary>
internal static class DesktopModules
{
    public static readonly IReadOnlyList<IDesktopModule> All = [new FilesModule()];
}

/// <summary>Names of the modules registered on this device — what <c>capabilities()</c> answers.</summary>
internal sealed record ActiveModules(IReadOnlyList<string> Names);

/// <summary>Requests from bridge handlers that only the window can carry out.</summary>
internal sealed class ShellCommands
{
    public event Action? ChangeServerRequested;
    public event Action? RestartRequested;

    /// <summary>Set when the app should start again once it has closed (a module was switched on or off).</summary>
    public bool RestartPending { get; private set; }

    public void RequestChangeServer() => ChangeServerRequested?.Invoke();

    public void RequestRestart()
    {
        RestartPending = true;
        RestartRequested?.Invoke();
    }
}

internal static class AppInfo
{
    /// <summary>The <c>/VERSION</c> the app was built with, without the build metadata after <c>+</c>.</summary>
    public static readonly string Version =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    public static Icon LoadIcon()
    {
        using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("pandora.ico")!;
        return new Icon(stream);
    }
}

/// <summary>The few strings the shell draws itself (tray menu); everything else is the web's.</summary>
internal static class ShellText
{
    private static bool Pt => CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

    public static string Open => Pt ? "Abrir o Pandora" : "Open Pandora";
    public static string Settings => Pt ? "Configurações" : "Settings";
    public static string Quit => Pt ? "Sair" : "Quit";
    public static string PickFolder => Pt ? "Escolha uma pasta para o Pandora catalogar" : "Choose a folder for Pandora to catalog";
}
