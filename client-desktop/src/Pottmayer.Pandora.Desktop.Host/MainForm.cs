using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// The window: a WebView2 showing the user's Pandora, plus the tray icon. Closing hides to the tray;
/// only "Quit" ends the app.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly HashSet<CoreWebView2WebErrorStatus> Unreachable =
    [
        CoreWebView2WebErrorStatus.CannotConnect,
        CoreWebView2WebErrorStatus.ConnectionAborted,
        CoreWebView2WebErrorStatus.ConnectionReset,
        CoreWebView2WebErrorStatus.Disconnected,
        CoreWebView2WebErrorStatus.HostNameNotResolved,
        CoreWebView2WebErrorStatus.ServerUnreachable,
        CoreWebView2WebErrorStatus.Timeout,
    ];

    private readonly DesktopSettingsStore _settings;
    private readonly DeviceCredentialStore _credentials;
    private readonly Bridge _bridge;
    private readonly bool _startHidden;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly NotifyIcon _tray;
    private string? _bridgeScriptId;
    private bool _quitting;

    public MainForm(
        DesktopSettingsStore settings, DeviceCredentialStore credentials, Bridge bridge, ShellCommands commands, bool startHidden)
    {
        _settings = settings;
        _credentials = credentials;
        _bridge = bridge;
        _startHidden = startHidden;

        Text = "Pandora";
        Icon = AppInfo.LoadIcon();
        MinimumSize = new Size(800, 560);
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        if (startHidden) WindowState = FormWindowState.Minimized;
        Controls.Add(_web);

        _tray = new NotifyIcon
        {
            Icon = new Icon(Icon, SystemInformation.SmallIconSize),
            Text = "Pandora",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        commands.ChangeServerRequested += ShowSetup;
        commands.RestartRequested += () => BeginInvoke(Quit);
        Load += async (_, _) => await InitializeAsync();
    }

    private CoreWebView2 Core => _web.CoreWebView2;

    public void ShowWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_startHidden) Hide(); // started with Windows: straight to the tray
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _tray.Visible = false;
        base.OnFormClosing(e);
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(ShellText.Open, null, (_, _) => ShowWindow());
        menu.Items.Add(ShellText.Settings, null, (_, _) => OpenWebSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ShellText.Quit, null, (_, _) => Quit());
        return menu;
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    private async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(DesktopSettingsStore.DataDir, "WebView2"));
        await _web.EnsureCoreWebView2Async(environment);

        Core.SetVirtualHostNameToFolderMapping(
            ShellUrls.ShellHost, Path.Combine(AppContext.BaseDirectory, "shell"), CoreWebView2HostResourceAccessKind.DenyCors);
        Core.Settings.IsStatusBarEnabled = false;
        Core.NavigationStarting += OnNavigationStarting;
        Core.NavigationCompleted += OnNavigationCompleted;
        Core.NewWindowRequested += OnNewWindowRequested;
        Core.WebMessageReceived += OnWebMessageReceived;
        Core.DocumentTitleChanged += (_, _) =>
            Text = string.IsNullOrWhiteSpace(Core.DocumentTitle) ? "Pandora" : Core.DocumentTitle;

        _bridge.Post = json => BeginInvoke(() => Core.PostWebMessageAsJson(json));

        await InstallBridgeScriptAsync();
        NavigateHome();
    }

    /// <summary>The bridge script carries the allowed origin, so it is replaced whenever the server changes.</summary>
    private async Task InstallBridgeScriptAsync()
    {
        if (_bridgeScriptId is not null) Core.RemoveScriptToExecuteOnDocumentCreated(_bridgeScriptId);
        _bridgeScriptId = null;

        if (_settings.Server is { } server)
            _bridgeScriptId = await Core.AddScriptToExecuteOnDocumentCreatedAsync(
                BridgeScript.For(ShellUrls.Origin(server), AppInfo.Version));
    }

    private void NavigateHome()
    {
        if (_settings.Server is { } server) Core.Navigate(server.ToString());
        else ShowSetup();
    }

    private void ShowSetup() =>
        Core.Navigate(ShellUrls.ShellPage("setup.html", "?value=" + Uri.EscapeDataString(_settings.Current.ServerUrl ?? "")));

    private void OpenWebSettings()
    {
        ShowWindow();
        if (_settings.Server is { } server) Core.Navigate(new Uri(server, "/settings").ToString());
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsTrusted(e.Uri)) return;
        // A redirect is the server's doing (e.g. a sign-in gateway) — follow it. A link the user
        // clicked to another site opens in the default browser instead.
        if (e.IsRedirected || !ShellUrls.IsWeb(e.Uri)) return;
        e.Cancel = true;
        OpenInBrowser(e.Uri);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess || !Unreachable.Contains(e.WebErrorStatus)) return;
        if (ShellUrls.SameOrigin(Core.Source, ShellUrls.ShellOrigin)) return;
        Core.Navigate(ShellUrls.ShellPage("offline.html", "?url=" + Uri.EscapeDataString(_settings.Current.ServerUrl ?? "")));
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (_settings.Server is { } server && ShellUrls.SameOrigin(e.Uri, server)) return;
        e.Handled = true;
        if (ShellUrls.IsWeb(e.Uri)) OpenInBrowser(e.Uri);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (ShellUrls.SameOrigin(e.Source, ShellUrls.ShellOrigin))
        {
            await HandleShellPageMessageAsync(e.WebMessageAsJson);
            return;
        }

        var reply = await _bridge.HandleAsync(_settings.Server, e.Source, e.WebMessageAsJson, CancellationToken.None);
        if (reply is not null) Core.PostWebMessageAsJson(reply);
    }

    /// <summary>Messages from the shell's own pages (first run, offline): <c>{ type, url? }</c>.</summary>
    private async Task HandleShellPageMessageAsync(string json)
    {
        using var message = JsonDocument.Parse(json);
        var root = message.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

        switch (type)
        {
            case "setServer":
                var input = root.TryGetProperty("url", out var u) ? u.GetString() : null;
                if (ShellUrls.TryParseServer(input) is not { } server)
                {
                    Core.Navigate(ShellUrls.ShellPage("setup.html", "?invalid=1&value=" + Uri.EscapeDataString(input ?? "")));
                    return;
                }
                // A device key belongs to the server that issued it.
                if (_settings.Server is not { } previous || ShellUrls.Origin(previous) != ShellUrls.Origin(server))
                    _credentials.Forget();
                _settings.Current.ServerUrl = server.ToString();
                _settings.Save();
                await InstallBridgeScriptAsync();
                NavigateHome();
                break;
            case "retry":
                NavigateHome();
                break;
            case "changeServer":
                ShowSetup();
                break;
        }
    }

    private bool IsTrusted(string uri) =>
        ShellUrls.SameOrigin(uri, ShellUrls.ShellOrigin)
        || (_settings.Server is { } server && ShellUrls.SameOrigin(uri, server));

    private static void OpenInBrowser(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Could not open '{uri}' in the browser: {ex.Message}");
        }
    }
}
