using System.Text.Json;

namespace Pottmayer.Pandora.Desktop.Abstractions;

/// <summary>A bridge method backed by a function, for modules whose handlers are a few lines each.</summary>
public sealed class BridgeMethod(string method, Func<JsonElement?, CancellationToken, Task<object?>> handle) : IBridgeHandler
{
    public string Method => method;

    public Task<object?> HandleAsync(JsonElement? args, CancellationToken cancellationToken) => handle(args, cancellationToken);
}
