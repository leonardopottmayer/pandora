using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Desktop.Abstractions;
using Velopack;

namespace Pottmayer.Pandora.Desktop.Host;

internal static class Program
{
    private const string InstanceName = @"Local\Pottmayer.Pandora.Desktop";

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
        builder.Services.AddSingleton(active);
        builder.Services.AddSingleton<ShellCommands>();
        builder.Services.AddSingleton<Bridge>();
        builder.Services.AddSingleton<IBridgeEvents>(sp => sp.GetRequiredService<Bridge>());
        AddShellBridgeMethods(builder.Services);
        builder.Services.AddSingleton(sp => new MainForm(
            sp.GetRequiredService<DesktopSettingsStore>(),
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

        services.AddSingleton<IBridgeHandler>(sp => new DelegateBridgeHandler(
            "desktop.changeServer", _ =>
            {
                sp.GetRequiredService<ShellCommands>().RequestChangeServer();
                return null;
            }));
    }
}
