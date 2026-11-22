using System.Text.Json;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>This PC's settings, in <c>%LOCALAPPDATA%\Pandora\settings.json</c>.</summary>
internal sealed class DesktopSettings
{
    public string? ServerUrl { get; set; }

    /// <summary>The device switch of each desktop module, by module name. Missing means off.</summary>
    public Dictionary<string, bool> Modules { get; set; } = [];
}

internal sealed class DesktopSettingsStore
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pandora");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;

    public DesktopSettingsStore(string? dataDir = null)
    {
        _path = Path.Combine(dataDir ?? DataDir, "settings.json");
        Current = Load(_path);
    }

    public DesktopSettings Current { get; }

    public Uri? Server => ShellUrls.TryParseServer(Current.ServerUrl);

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, Json));
        File.Move(temp, _path, overwrite: true);
    }

    private static DesktopSettings Load(string path)
    {
        if (!File.Exists(path)) return new DesktopSettings();
        try
        {
            return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path), Json) ?? new DesktopSettings();
        }
        catch (JsonException)
        {
            // A corrupt file only costs the server URL: first run asks again.
            return new DesktopSettings();
        }
    }
}
