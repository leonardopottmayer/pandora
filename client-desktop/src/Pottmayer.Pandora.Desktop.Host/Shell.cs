using System.Globalization;
using System.Reflection;
using Pottmayer.Pandora.Desktop.Abstractions;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// The shell's whole knowledge of features: a list. It stays empty until the first desktop module
/// (Files, phase F1) — adding one is a line here, never a change to the shell.
/// </summary>
internal static class DesktopModules
{
    public static readonly IReadOnlyList<IDesktopModule> All = [];
}

/// <summary>Names of the modules registered on this device — what <c>capabilities()</c> answers.</summary>
internal sealed record ActiveModules(IReadOnlyList<string> Names);

/// <summary>Requests from bridge handlers that only the window can carry out.</summary>
internal sealed class ShellCommands
{
    public event Action? ChangeServerRequested;

    public void RequestChangeServer() => ChangeServerRequested?.Invoke();
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
}
