using System.Diagnostics;
using System.Text.Json;
using Pottmayer.Pandora.Desktop.Abstractions;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// Routes <c>{ id, method, args }</c> messages from the web to the registered <see cref="IBridgeHandler"/>s
/// and answers <c>{ id, result }</c> or <c>{ id, error }</c>. Knows nothing about WebView2, so it is testable.
/// </summary>
internal sealed class Bridge : IBridgeEvents
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, IBridgeHandler> _handlers;

    public Bridge(IEnumerable<IBridgeHandler> handlers) =>
        // Duplicate method names throw here, at startup, not on the first call.
        _handlers = handlers.ToDictionary(h => h.Method, StringComparer.Ordinal);

    /// <summary>Sends a JSON message to the page; set by the window once the WebView exists.</summary>
    public Action<string>? Post { get; set; }

    /// <returns>The reply to post back, or null when the message is dropped (foreign origin, malformed).</returns>
    public async Task<string?> HandleAsync(Uri? allowedOrigin, string? source, string messageJson, CancellationToken cancellationToken)
    {
        if (allowedOrigin is null || !ShellUrls.SameOrigin(source, allowedOrigin))
        {
            Trace.TraceWarning($"Bridge call from '{source}' dropped: not the Pandora origin.");
            return null;
        }

        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(messageJson, Json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (request?.Method is not { Length: > 0 } method) return null;

        if (!_handlers.TryGetValue(method, out var handler))
            return Reply(request.Id, error: $"Unknown method '{method}'.");

        try
        {
            return Reply(request.Id, await handler.HandleAsync(request.Args, cancellationToken));
        }
        catch (Exception ex)
        {
            return Reply(request.Id, error: ex.Message);
        }
    }

    public void Publish(string eventName, object? payload) =>
        Post?.Invoke(JsonSerializer.Serialize(new { @event = eventName, payload }, Json));

    private static string Reply(long id, object? result = null, string? error = null) =>
        error is null
            ? JsonSerializer.Serialize(new { id, result }, Json)
            : JsonSerializer.Serialize(new { id, error }, Json);

    private sealed record BridgeRequest(long Id, string? Method, JsonElement? Args);
}

/// <summary>A bridge method backed by a function — enough for the shell's own <c>desktop.*</c> methods.</summary>
internal sealed class DelegateBridgeHandler(string method, Func<JsonElement?, object?> handle) : IBridgeHandler
{
    public string Method => method;

    public Task<object?> HandleAsync(JsonElement? args, CancellationToken cancellationToken) =>
        Task.FromResult(handle(args));
}
