using Microsoft.Win32;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>"Start with Windows": a value under the current user's Run key — no admin needed.</summary>
internal static class Autostart
{
    public const string HiddenArg = "--hidden";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PandoraDesktop";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {HiddenArg}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
