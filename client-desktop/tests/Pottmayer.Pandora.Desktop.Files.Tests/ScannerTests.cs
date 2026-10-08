using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Files.Agent;
using Xunit;

namespace Pottmayer.Pandora.Desktop.Files.Tests;

/// <summary>The scan protocol from the agent's side, against a fake server that behaves like the backend.</summary>
public sealed class ScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pandora-scan-").FullName;
    private readonly FakeServer _server = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private Scanner Scanner => new(new FilesApi(_server));

    private RootConfig Root(string? localPath = null) =>
        new(Guid.NewGuid(), "Disk", localPath ?? _root, false, false, null, null, [], []);

    [Fact]
    public async Task Every_walked_entry_is_sent_and_files_are_fingerprinted_when_asked()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Movies"));
        File.WriteAllText(Path.Combine(_root, "Movies", "a.mkv"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        var progress = new List<ScanProgress>();

        var outcome = await Scanner.RunAsync(Root(), progress.Add, CancellationToken.None);

        Assert.Equal("completed", outcome.Status);
        Assert.Equal(3, _server.EntriesSeen);
        Assert.Equal(["/Movies/a.mkv", "/b.txt"], _server.Fingerprints.Keys.Order(StringComparer.Ordinal));
        Assert.All(_server.Fingerprints.Values, f => Assert.True(Fingerprint.IsValid(f)));
        Assert.Equal(2, progress[^1].Fingerprinted);
    }

    [Fact]
    public async Task A_missing_root_aborts_the_scan_instead_of_reporting_it_empty()
    {
        var outcome = await Scanner.RunAsync(Root(Path.Combine(_root, "unplugged")), _ => { }, CancellationToken.None);

        Assert.Equal("aborted", outcome.Status);
        Assert.Equal("root-unavailable", _server.AbortReason);
        Assert.Null(_server.EntriesSeen);
    }

    [Fact]
    public async Task A_failure_mid_scan_aborts_it_on_the_server()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        _server.FailBatches = true;

        await Assert.ThrowsAsync<FilesApiException>(() => Scanner.RunAsync(Root(), _ => { }, CancellationToken.None));

        Assert.Equal("agent-error", _server.AbortReason);
    }

    /// <summary>Answers the agent endpoints like the backend: asks for the fingerprint of every file sent without one.</summary>
    private sealed class FakeServer : HttpMessageHandler, IHttpClientFactory
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly Guid _scanId = Guid.NewGuid();

        public Dictionary<string, string> Fingerprints { get; } = [];
        public int? EntriesSeen { get; private set; }
        public string? AbortReason { get; private set; }
        public bool FailBatches { get; set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("https://pandora.test/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/scans")) return Ok(new StartScanResponse(_scanId));

            if (path.EndsWith("/batches"))
            {
                if (FailBatches) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                var batch = (await request.Content!.ReadFromJsonAsync<ScanBatch>(Json, ct))!;
                foreach (var e in batch.Entries.Where(e => e.Fingerprint is not null)) Fingerprints[e.Path] = e.Fingerprint!;
                return Ok(new ScanBatchResult([.. batch.Entries.Where(e => e.Kind == AgentValues.File && e.Fingerprint is null).Select(e => e.Path)]));
            }

            if (path.EndsWith("/complete"))
            {
                EntriesSeen = (await request.Content!.ReadFromJsonAsync<CompleteScanRequest>(Json, ct))!.EntriesSeen;
                return Ok(Outcome("completed"));
            }

            AbortReason = (await request.Content!.ReadFromJsonAsync<AbortScanRequest>(Json, ct))!.Reason;
            return Ok(Outcome("aborted"));
        }

        private ScanOutcome Outcome(string status) => new(_scanId, status, EntriesSeen ?? 0, 0, 0, 0, 0, 0, null);

        private static HttpResponseMessage Ok(object data) => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { data }, options: Json) };
    }
}
