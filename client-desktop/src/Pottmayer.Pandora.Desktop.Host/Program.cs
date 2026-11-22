using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Desktop.Abstractions;
using Velopack;

namespace Pottmayer.Pandora.Desktop.Host;

internal static class Program
{
    private const string InstanceName = @"Local\Pottmayer.Pandora.Desktop";

    /// <summary>Passed to the new process when the app restarts itself: it waits for the old one to exit.</summary>
    private const string RestartArg = "--restart";

    [STAThread]
    private static void Main(string[] args)
    {
        // Must run first: Velopack's install/update/uninstall hooks exit the process here when they apply.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Autostart.Set(false))
            .Run();

        // Single instance: a second launch only asks the first one to show its window.
        using var instance = new Mutex(initiallyOwned: true, InstanceName, out var isFirst);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!isFirst && args.Contains(RestartArg)) isFirst = WaitForPreviousInstance(instance);
        if (!isFirst)
        {
            showSignal.Set();
            return;
        }

        ApplicationConfiguration.Initialize();

        var settings = new DesktopSettingsStore();
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
        var active = RegisterModules(builder.Services, settings.Current);

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<DeviceCredentialStore>();
        builder.Services.AddSingleton(active);
        builder.Services.AddSingleton<ShellCommands>();
        builder.Services.AddSingleton<Bridge>();
        builder.Services.AddSingleton<IBridgeEvents>(sp => sp.GetRequiredService<Bridge>());
        builder.Services.AddSingleton<IDesktopShell, WindowsShell>();
        AddShellBridgeMethods(builder.Services);
        builder.Services.AddDeviceHttpClient();
        builder.Services.AddSingleton(sp => new MainForm(
            sp.GetRequiredService<DesktopSettingsStore>(),
            sp.GetRequiredService<DeviceCredentialStore>(),
            sp.GetRequiredService<Bridge>(),
            sp.GetRequiredService<ShellCommands>(),
            startHidden: args.Contains(Autostart.HiddenArg)));

        using var host = builder.Build();
        host.Start();

        var form = host.Services.GetRequiredService<MainForm>();
        new Thread(() =>
        {
            while (showSignal.WaitOne())
                if (form.IsHandleCreated) form.BeginInvoke(form.ShowWindow);
        }) { IsBackground = true }.Start();

        using var stopping = new CancellationTokenSource();
        _ = Updater.RunAsync(stopping.Token);

        Application.Run(form);

        stopping.Cancel();
        host.StopAsync().GetAwaiter().GetResult();

        instance.ReleaseMutex();
        if (host.Services.GetRequiredService<ShellCommands>().RestartPending)
            Process.Start(Environment.ProcessPath!, RestartArg);
    }

    /// <summary>A restart starts the new process before the old one has fully exited; it waits its turn.</summary>
    private static bool WaitForPreviousInstance(Mutex instance)
    {
        try
        {
            return instance.WaitOne(TimeSpan.FromSeconds(30));
        }
        catch (AbandonedMutexException)
        {
            return true; // the old process ended without releasing it: ours now
        }
    }

    /// <summary>Registers only the modules switched on for this device; the rest do not exist at runtime.</summary>
    private static ActiveModules RegisterModules(IServiceCollection services, DesktopSettings settings)
    {
        var names = new List<string>();
        foreach (var module in DesktopModules.All)
        {
            if (!settings.Modules.GetValueOrDefault(module.Name)) continue;
            module.Register(services);
            names.Add(module.Name);
        }
        return new ActiveModules(names);
    }

    /// <summary>The shell's own namespace, <c>desktop.*</c>.</summary>
    private static void AddShellBridgeMethods(IServiceCollection services)
    {
        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.capabilities", _ => sp.GetRequiredService<ActiveModules>().Names));

        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.getSettings", _ => new
            {
                version = AppInfo.Version,
                serverUrl = sp.GetRequiredService<DesktopSettingsStore>().Current.ServerUrl,
                autostart = Autostart.IsEnabled(),
                machineName = Environment.MachineName,
                platform = "windows",
                form = "desktop",
                deviceId = sp.GetRequiredService<DeviceCredentialStore>().Load()?.DeviceId,
            }));

        // Pairing: the web (signed in) registers the device and hands its key over, once.
        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.storeCredential", args =>
            {
                var deviceId = args?.GetProperty("deviceId").GetGuid() ?? throw new ArgumentException("deviceId is required.");
                var key = args.Value.GetProperty("key").GetString();
                if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key is required.");
                sp.GetRequiredService<DeviceCredentialStore>().Save(new DeviceCredential(deviceId, key));
                return true;
            }));

        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.forgetCredential", _ =>
            {
                sp.GetRequiredService<DeviceCredentialStore>().Forget();
                return true;
            }));

        services.AddSingleton<IBridgeHandler>(new DelegateBridgeHandler(
            "desktop.setAutostart", args =>
            {
                var enabled = args is { ValueKind: JsonValueKind.Object } a
                    && a.TryGetProperty("enabled", out var e)
                    && e.ValueKind == JsonValueKind.True;
                Autostart.Set(enabled);
                return Autostart.IsEnabled();
            }));

        // The device switch of each module: whether this PC does that feature's native work.
        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.getModules", _ => DesktopModules.All.Select(m => new
            {
                name = m.Name,
                enabled = sp.GetRequiredService<DesktopSettingsStore>().Current.Modules.GetValueOrDefault(m.Name),
            })));

        // A switched-off module has no services at all, so turning one on or off restarts the app.
        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.setModule", args =>
            {
                var name = args is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (DesktopModules.All.All(m => m.Name != name)) throw new ArgumentException($"Unknown module '{name}'.");
                var enabled = args!.Value.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True;

                var settings = sp.GetRequiredService<DesktopSettingsStore>();
                if (settings.Current.Modules.GetValueOrDefault(name!) == enabled) return new { restarting = false };

                settings.Current.Modules[name!] = enabled;
                settings.Save();
                sp.GetRequiredService<ShellCommands>().RequestRestart();
                return new { restarting = true };
            }));

        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.changeServer", _ =>
            {
                sp.GetRequiredService<ShellCommands>().RequestChangeServer();
                return null;
            }));
    }
}
