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
    public async Task Every_walked_entry_is_sent_and_files_are_fingerprinted_and_read_when_asked()
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
        // Only the readable extension is read; a file that is not really a video says nothing.
        Assert.Equal(new FileMetadata(), Assert.Single(_server.Metadata, m => m.Key == "/Movies/a.mkv").Value);
        Assert.Single(_server.Metadata);
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

    [Fact]
    public async Task A_page_instead_of_the_api_is_an_error_that_says_so()
    {
        _server.AnswerWithPage = true;

        var error = await Assert.ThrowsAsync<FilesApiException>(() => new FilesApi(_server).GetConfigAsync(CancellationToken.None));

        Assert.Contains("answered with a page", error.Message);
    }

    /// <summary>
    /// Answers the agent endpoints like the backend: asks for the fingerprint of every file sent without
    /// one, and for the metadata of every readable file sent without it.
    /// </summary>
    private sealed class FakeServer : HttpMessageHandler, IHttpClientFactory
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly Guid _scanId = Guid.NewGuid();

        public Dictionary<string, string> Fingerprints { get; } = [];
        public Dictionary<string, FileMetadata> Metadata { get; } = [];
        public int? EntriesSeen { get; private set; }
        public string? AbortReason { get; private set; }
        public bool FailBatches { get; set; }

        /// <summary>Behaves like a web server that does not proxy /api: the SPA's index for any GET.</summary>
        public bool AnswerWithPage { get; set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("https://pandora.test/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (AnswerWithPage) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<!doctype html>", null, "text/html") };
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/scans")) return Ok(new StartScanResponse(_scanId));

            if (path.EndsWith("/batches"))
            {
                if (FailBatches) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                var batch = (await request.Content!.ReadFromJsonAsync<ScanBatch>(Json, ct))!;
                foreach (var e in batch.Entries.Where(e => e.Fingerprint is not null)) Fingerprints[e.Path] = e.Fingerprint!;
                foreach (var e in batch.Entries.Where(e => e.Metadata is not null)) Metadata[e.Path] = e.Metadata!;
                var files = batch.Entries.Where(e => e.Kind == AgentValues.File).ToList();
                return Ok(new ScanBatchResult(
                    [.. files.Where(e => e.Fingerprint is null && !Fingerprints.ContainsKey(e.Path)).Select(e => e.Path)],
                    [.. files.Where(e => e.Metadata is null && !Metadata.ContainsKey(e.Path)
                                         && FileMetadata.IsReadable(CatalogPath.Extension(CatalogPath.Name(e.Path)))).Select(e => e.Path)]));
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
