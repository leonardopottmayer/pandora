using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pottmayer.Pandora.Desktop.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>The agent side of the scan protocol (product-plan §4.4), as this device.</summary>
internal sealed class FilesApi(IHttpClientFactory http)
{
    private const string Base = "api/v1/files/agent/";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<AgentConfig> GetConfigAsync(CancellationToken ct) =>
        SendAsync<AgentConfig>(HttpMethod.Get, "config", null, ct);

    public async Task<Guid> StartScanAsync(Guid rootId, CancellationToken ct) =>
        (await SendAsync<StartScanResponse>(HttpMethod.Post, "scans", new StartScanRequest(rootId), ct)).ScanId;

    public Task<ScanBatchResult> SendBatchAsync(Guid scanId, IReadOnlyList<ScannedEntry> entries, CancellationToken ct) =>
        SendAsync<ScanBatchResult>(HttpMethod.Post, $"scans/{scanId}/batches", new ScanBatch(entries), ct);

    public Task<ScanOutcome> CompleteAsync(Guid scanId, int entriesSeen, CancellationToken ct) =>
        SendAsync<ScanOutcome>(HttpMethod.Post, $"scans/{scanId}/complete", new CompleteScanRequest(entriesSeen), ct);

    public Task<ScanOutcome> AbortAsync(Guid scanId, string reason, CancellationToken ct) =>
        SendAsync<ScanOutcome>(HttpMethod.Post, $"scans/{scanId}/abort", new AbortScanRequest(reason), ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Base + uri);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        using var response = await http.CreateClient(DesktopHttp.DeviceClient).SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new FilesApiException(response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, ct);
        return envelope is { Data: { } data } ? data : throw new FilesApiException(response.StatusCode, "Empty response.");
    }

    private sealed record Envelope<T>(T? Data);
}

/// <summary>How the backend left a scan: <c>completed</c>, <c>held</c> (safety brake) or <c>aborted</c>.</summary>
internal sealed record ScanOutcome(
    Guid Id, string Status, int Seen, int Created, int Changed, int Moved, int Missing, int Excluded, string? Error);

internal sealed class FilesApiException(HttpStatusCode status, string body)
    : Exception($"Pandora answered {(int)status}: {(body.Length > MaxBody ? body[..MaxBody] + "…" : body)}")
{
    /// <summary>Enough of the body to explain the error, short enough for the page (a proxy may answer with a whole HTML page).</summary>
    private const int MaxBody = 200;

    public HttpStatusCode Status { get; } = status;

    /// <summary>The PC is not paired, or its key was revoked.</summary>
    public bool IsUnauthorized => Status == HttpStatusCode.Unauthorized;
}
