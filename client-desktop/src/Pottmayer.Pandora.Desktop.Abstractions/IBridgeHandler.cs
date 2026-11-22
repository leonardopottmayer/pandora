using System.Text.Json;

namespace Pottmayer.Pandora.Desktop.Abstractions;

/// <summary>
/// One method the web can call through <c>window.pandoraDesktop.invoke(method, args)</c>.
/// Calls only reach a handler when they come from the configured Pandora origin.
/// </summary>
public interface IBridgeHandler
{
    /// <summary>Namespaced by module: <c>files.pickFolder</c>. The shell owns <c>desktop.*</c>.</summary>
    string Method { get; }

    /// <summary>Handles the call. The result is serialized as JSON; an exception becomes the call's error.</summary>
    Task<object?> HandleAsync(JsonElement? args, CancellationToken cancellationToken);
}

/// <summary>Pushes events to the web, received by <c>window.pandoraDesktop.on(event, handler)</c>.</summary>
public interface IBridgeEvents
{
    void Publish(string eventName, object? payload);
}
