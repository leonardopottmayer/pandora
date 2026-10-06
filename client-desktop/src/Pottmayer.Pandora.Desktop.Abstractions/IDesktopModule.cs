using Microsoft.Extensions.DependencyInjection;

namespace Pottmayer.Pandora.Desktop.Abstractions;

/// <summary>
/// A native feature of Pandora Desktop (e.g. the Files agent). The shell keeps a fixed list of modules
/// and calls <see cref="Register"/> only for the ones switched on for this device — a module that is off
/// has no services, no bridge methods and no background work.
/// </summary>
public interface IDesktopModule
{
    /// <summary>Stable name: the bridge namespace (<c>files.*</c>), the settings key and the capability reported to the web.</summary>
    string Name { get; }

    /// <summary>Registers the module's hosted services, <see cref="IBridgeHandler"/>s and options.</summary>
    void Register(IServiceCollection services);
}
